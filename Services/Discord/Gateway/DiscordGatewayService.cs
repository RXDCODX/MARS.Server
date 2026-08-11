using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;
using ServerDiscordConfiguration = MARS.Server.Configuration.DiscordConfiguration;
#if USE_LOCAL_DSHARPPLUS_VOICE
using DSharpPlus.Voice;
#else
using DSharpPlus.VoiceNext;
#endif

namespace MARS.Server.Services.Discord.Gateway;

public class DiscordGatewayService(
    IOptions<ServerDiscordConfiguration> configuration,
    ILogger<DiscordGatewayService> logger,
    IMediaCompressor compressor
) : IDiscordGatewayService, IHostedService
{
    private const long DefaultUploadLimitBytes = 25L * 1024 * 1024;
    private const long Tier2UploadLimitBytes = 50L * 1024 * 1024;
    private const long Tier3UploadLimitBytes = 100L * 1024 * 1024;
    private const long SafetyMarginBytes = 512 * 1024;

    private readonly ServerDiscordConfiguration _configuration = configuration.Value;
    private readonly List<Func<DiscordClient, MessageCreatedEventArgs, Task>> _messageHandlers = [];
    private readonly List<
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task>
    > _voiceStateHandlers = [];
    private readonly List<
        Func<DiscordClient, InteractionCreatedEventArgs, Task>
    > _interactionHandlers = [];
    private readonly List<
        Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task>
    > _componentInteractionHandlers = [];
    private readonly Lock _handlersLock = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public bool IsConnected { get; private set; }

    public DiscordClient? Client { get; private set; }

    public void RegisterMessageCreatedHandler(
        Func<DiscordClient, MessageCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _messageHandlers.Add(handler);
        }
    }

    public void RegisterVoiceStateUpdatedHandler(
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _voiceStateHandlers.Add(handler);
        }
    }

    public void RegisterInteractionCreatedHandler(
        Func<DiscordClient, InteractionCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _interactionHandlers.Add(handler);
        }
    }

    public void RegisterComponentInteractionCreatedHandler(
        Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task> handler
    )
    {
        lock (_handlersLock)
        {
            _componentInteractionHandlers.Add(handler);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Token))
        {
            logger.LogWarning("Discord token не задан, интеграция Discord отключена");
        }
        else
        {
            await EnsureConnectedAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Client is not null)
        {
            try
            {
                await Client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при отключении Discord клиента");
            }
            finally
            {
                IsConnected = false;
                Client = null;
            }
        }
    }

    public async Task<OperationResult> SendMessageAsync(
        ulong channelId,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Discord клиент недоступен");

        if (channelId != 0 && !string.IsNullOrWhiteSpace(message))
        {
            try
            {
                var client = await EnsureConnectedAsync(cancellationToken);
                if (client is not null)
                {
                    var channel = await client.GetChannelAsync(channelId);
                    if (channel is not null)
                    {
                        await channel.SendMessageAsync(message);
                        result = OperationResult.Ok("Сообщение отправлено в Discord");
                    }
                    else
                    {
                        result = OperationResult.Bad("Discord канал не найден");
                    }
                }
                else
                {
                    result = OperationResult.Bad("Не удалось инициализировать Discord клиент");
                }
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                logger.LogError(
                    ex,
                    "Discord канал {ChannelId} не найден (удалён или нет доступа)",
                    channelId
                );
                result = OperationResult.Bad($"Discord канал {channelId} не найден");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка отправки сообщения в Discord канал {ChannelId}",
                    channelId
                );
                result = OperationResult.Bad($"Ошибка Discord отправки: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Неверные параметры для отправки в Discord");
        }

        return result;
    }

    public async Task<OperationResult> SendFileAsync(
        ulong channelId,
        Stream fileStream,
        string fileName,
        string? message = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Discord клиент недоступен");

        if (channelId != 0 && fileStream is { Length: > 0 })
        {
            try
            {
                var client = await EnsureConnectedAsync(cancellationToken);
                if (client is not null)
                {
                    var channel = await client.GetChannelAsync(channelId);
                    if (channel is not null)
                    {
                        var maxSize = GetMaxUploadSize(channel);

                        if (fileStream.Length > maxSize)
                        {
                            result = await HandleLargeFileAsync(
                                channel,
                                fileStream,
                                fileName,
                                message,
                                maxSize,
                                cancellationToken
                            );
                        }
                        else
                        {
                            var builder = new DiscordMessageBuilder();
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                builder.WithContent(message);
                            }

                            builder.AddFile(fileName, fileStream, true);
                            await channel.SendMessageAsync(builder);
                            result = OperationResult.Ok("Файл отправлен в Discord");
                        }
                    }
                    else
                    {
                        result = OperationResult.Bad("Discord канал не найден");
                    }
                }
                else
                {
                    result = OperationResult.Bad("Не удалось инициализировать Discord клиент");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка отправки файла в Discord канал {ChannelId}", channelId);
                result = OperationResult.Bad($"Ошибка Discord отправки файла: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Неверные параметры для отправки файла в Discord");
        }

        return result;
    }

    public async Task<OperationResult> SendFilesAsync(
        ulong channelId,
        IReadOnlyList<(Stream Stream, string FileName)> files,
        string? message = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Discord клиент недоступен");

        if (channelId != 0 && files.Count > 0)
        {
            try
            {
                var client = await EnsureConnectedAsync(cancellationToken);
                if (client is not null)
                {
                    var channel = await client.GetChannelAsync(channelId);
                    if (channel is not null)
                    {
                        var maxSize = GetMaxUploadSize(channel);
                        var preparedFiles = new List<(Stream Stream, string FileName)>();

                        foreach (var (stream, name) in files)
                        {
                            if (stream.Length > maxSize)
                            {
                                var prepared = await TryPrepareFileAsync(
                                    stream,
                                    name,
                                    maxSize,
                                    cancellationToken
                                );

                                if (prepared is not null)
                                {
                                    preparedFiles.Add(
                                        (prepared.Value.Stream, prepared.Value.FileName)
                                    );
                                }
                            }
                            else
                            {
                                preparedFiles.Add((stream, name));
                            }
                        }

                        if (preparedFiles.Count > 0)
                        {
                            var builder = new DiscordMessageBuilder();

                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                builder.WithContent(message);
                            }

                            foreach (var (stream, name) in preparedFiles)
                            {
                                builder.AddFile(name, stream, true);
                            }

                            await channel.SendMessageAsync(builder);
                            result = OperationResult.Ok("Файлы отправлены в Discord");
                        }
                        else
                        {
                            logger.LogInformation(
                                "Все файлы альбома пропущены: превышен лимит Discord"
                            );
                            result = OperationResult.Ok("Все файлы пропущены");
                        }
                    }
                    else
                    {
                        result = OperationResult.Bad("Discord канал не найден");
                    }
                }
                else
                {
                    result = OperationResult.Bad("Не удалось инициализировать Discord клиент");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ошибка отправки файлов в Discord канал {ChannelId}",
                    channelId
                );
                result = OperationResult.Bad($"Ошибка Discord отправки файлов: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Неверные параметры для отправки файлов в Discord");
        }

        return result;
    }

    public async Task<DiscordClient?> EnsureConnectedAsync(
        CancellationToken cancellationToken = default
    )
    {
        DiscordClient? result = Client;

        if (result is null)
        {
            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                result = Client;
                if (result is null)
                {
                    var intents =
                        DiscordIntents.Guilds
                        | DiscordIntents.GuildMessages
                        | DiscordIntents.MessageContents
                        | DiscordIntents.GuildVoiceStates;

                    var builder = DiscordClientBuilder
                        .CreateDefault(_configuration.Token, intents)
                        .ConfigureEventHandlers(events =>
                        {
                            events.HandleMessageCreated(HandleMessageCreatedAsync);
                            events.HandleVoiceStateUpdated(HandleVoiceStateUpdatedAsync);
                            events.HandleInteractionCreated(HandleInteractionCreatedAsync);
                            events.HandleComponentInteractionCreated(
                                HandleComponentInteractionCreatedAsync
                            );
                        })
#if USE_LOCAL_DSHARPPLUS_VOICE
                        .UseVoice()
#else
                        .UseVoiceNext(new VoiceNextConfiguration())
#endif
                        .SetLogLevel(LogLevel.Information);

                    result = builder.Build();
                    await result.ConnectAsync();

                    Client = result;
                    IsConnected = true;

                    logger.LogInformation("Discord клиент подключен");
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                _connectLock.Release();
            }
        }

        return result;
    }

    private async Task<OperationResult> HandleLargeFileAsync(
        DiscordChannel channel,
        Stream fileStream,
        string fileName,
        string? message,
        long maxSize,
        CancellationToken ct
    )
    {
        var result = OperationResult.Ok("Файл пропущен");

        if (VideoExtensions.IsVideoFile(fileName))
        {
            var segments = await compressor.CompressVideoAsync(fileStream, fileName, maxSize, ct);

            if (segments is null || segments.Count == 0)
            {
                logger.LogInformation(
                    "Видео {FileName} ({Size}MB) пропущено: не удалось сжать до лимита {Limit}MB",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0,
                    maxSize / 1024 / 1024
                );
                return result;
            }

            foreach (var (segment, segName) in segments)
            {
                await using var seg = segment;
                var segBuilder = new DiscordMessageBuilder();

                if (!string.IsNullOrWhiteSpace(message))
                {
                    segBuilder.WithContent(message);
                }

                segBuilder.AddFile(segName, seg, true);
                await channel.SendMessageAsync(segBuilder);
            }

            result = OperationResult.Ok($"Видео отправлено {segments.Count} сегментами");
        }
        else if (VideoExtensions.IsImageFile(fileName))
        {
            var compressed = await compressor.CompressImageAsync(fileStream, fileName, maxSize, ct);

            if (compressed is null)
            {
                logger.LogInformation(
                    "Изображение {FileName} ({Size}MB) пропущено: не удалось сжать до лимита {Limit}MB",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0,
                    maxSize / 1024 / 1024
                );
                return result;
            }

            await using var cmp = compressed;
            var builder = new DiscordMessageBuilder();

            if (!string.IsNullOrWhiteSpace(message))
            {
                builder.WithContent(message);
            }

            builder.AddFile(fileName, cmp, true);
            await channel.SendMessageAsync(builder);
            result = OperationResult.Ok("Файл сжат и отправлен в Discord");
        }
        else if (VideoExtensions.IsAudioFile(fileName))
        {
            var compressed = await compressor.CompressAudioAsync(fileStream, fileName, maxSize, ct);

            if (compressed is null)
            {
                logger.LogInformation(
                    "Аудио {FileName} ({Size}MB) пропущено: не удалось сжать до лимита {Limit}MB",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0,
                    maxSize / 1024 / 1024
                );
                return result;
            }

            await using var cmp = compressed;
            var builder = new DiscordMessageBuilder();

            if (!string.IsNullOrWhiteSpace(message))
            {
                builder.WithContent(message);
            }

            builder.AddFile(fileName, cmp, true);
            await channel.SendMessageAsync(builder);
            result = OperationResult.Ok("Файл сжат и отправлен в Discord");
        }
        else
        {
            logger.LogInformation(
                "Файл {FileName} ({Size}MB) пропущен: не поддаётся сжатию, превышен лимит {Limit}MB",
                fileName,
                fileStream.Length / 1024.0 / 1024.0,
                maxSize / 1024 / 1024
            );
        }

        return result;
    }

    private async Task<(Stream Stream, string FileName)?> TryPrepareFileAsync(
        Stream fileStream,
        string fileName,
        long maxSize,
        CancellationToken ct
    )
    {
        if (VideoExtensions.IsVideoFile(fileName))
        {
            var segments = await compressor.CompressVideoAsync(fileStream, fileName, maxSize, ct);

            if (segments is null || segments.Count == 0)
            {
                logger.LogInformation(
                    "Файл {FileName} ({Size}MB) пропущен в альбоме",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0
                );
                return null;
            }

            return (segments[0].Stream, segments[0].FileName);
        }

        if (VideoExtensions.IsImageFile(fileName))
        {
            var compressed = await compressor.CompressImageAsync(fileStream, fileName, maxSize, ct);

            if (compressed is null)
            {
                logger.LogInformation(
                    "Изображение {FileName} ({Size}MB) пропущено в альбоме",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0
                );
                return null;
            }

            return (compressed, fileName);
        }

        if (VideoExtensions.IsAudioFile(fileName))
        {
            var compressed = await compressor.CompressAudioAsync(fileStream, fileName, maxSize, ct);

            if (compressed is null)
            {
                logger.LogInformation(
                    "Аудио {FileName} ({Size}MB) пропущено в альбоме",
                    fileName,
                    fileStream.Length / 1024.0 / 1024.0
                );
                return null;
            }

            return (compressed, fileName);
        }

        logger.LogInformation(
            "Файл {FileName} ({Size}MB) пропущен в альбоме: не поддаётся сжатию",
            fileName,
            fileStream.Length / 1024.0 / 1024.0
        );
        return null;
    }

    private static long GetMaxUploadSize(DiscordChannel? channel)
    {
        var result = DefaultUploadLimitBytes;

        if (channel?.Guild is { } guild)
        {
            result = guild.PremiumTier switch
            {
                DiscordPremiumTier.Tier_2 => Tier2UploadLimitBytes,
                DiscordPremiumTier.Tier_3 => Tier3UploadLimitBytes,
                _ => DefaultUploadLimitBytes,
            };
        }

        return result - SafetyMarginBytes;
    }

    private async Task HandleMessageCreatedAsync(DiscordClient client, MessageCreatedEventArgs args)
    {
        List<Func<DiscordClient, MessageCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _messageHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки MessageCreated обработчиком");
            }
        }
    }

    private async Task HandleVoiceStateUpdatedAsync(
        DiscordClient client,
        VoiceStateUpdatedEventArgs args
    )
    {
        List<Func<DiscordClient, VoiceStateUpdatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _voiceStateHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки VoiceStateUpdated обработчиком");
            }
        }
    }

    private async Task HandleInteractionCreatedAsync(
        DiscordClient client,
        InteractionCreatedEventArgs args
    )
    {
        List<Func<DiscordClient, InteractionCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _interactionHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки InteractionCreated обработчиком");
            }
        }
    }

    private async Task HandleComponentInteractionCreatedAsync(
        DiscordClient client,
        ComponentInteractionCreatedEventArgs args
    )
    {
        List<Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task>> handlersCopy;

        lock (_handlersLock)
        {
            handlersCopy = [.. _componentInteractionHandlers];
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                await handler(client, args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки ComponentInteractionCreated обработчиком");
            }
        }
    }
}
