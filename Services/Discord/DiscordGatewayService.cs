using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using ServerDiscordConfiguration = MARS.Server.Configuration.DiscordConfiguration;

namespace MARS.Server.Services.Discord;

public class DiscordGatewayService(
    IOptions<ServerDiscordConfiguration> configuration,
    ILogger<DiscordGatewayService> logger
) : IDiscordGatewayService, IHostedService
{
    private readonly ServerDiscordConfiguration _configuration = configuration.Value;
    private readonly List<Func<DiscordClient, MessageCreatedEventArgs, Task>> _messageHandlers = [];
    private readonly List<Func<DiscordClient, VoiceStateUpdatedEventArgs, Task>> _voiceStateHandlers = [];
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка отправки сообщения в Discord канал {ChannelId}", channelId);
                result = OperationResult.Bad($"Ошибка Discord отправки: {ex.Message}");
            }
        }
        else
        {
            result = OperationResult.Bad("Неверные параметры для отправки в Discord");
        }

        return result;
    }

    private async Task<DiscordClient?> EnsureConnectedAsync(CancellationToken cancellationToken)
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
                        DiscordIntents.GuildMessages
                        | DiscordIntents.MessageContents
                        | DiscordIntents.GuildVoiceStates;

                    var builder = DiscordClientBuilder
                        .CreateDefault(_configuration.Token, intents)
                        .ConfigureEventHandlers(events =>
                        {
                            events.HandleMessageCreated(HandleMessageCreatedAsync);
                            events.HandleVoiceStateUpdated(HandleVoiceStateUpdatedAsync);
                        })
                        .UseVoiceNext(new VoiceNextConfiguration())
                        .SetLogLevel(LogLevel.Information);

                    result = builder.Build();
                    await result.ConnectAsync();

                    Client = result;
                    IsConnected = true;

                    logger.LogInformation("Discord клиент подключен");
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        return result;
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
}
