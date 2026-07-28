using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Telegram.WTelegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;
using TLMessage = TL.Message;

namespace MARS.Server.Services.Telegram.DiscordBridge;

public class TelegramDiscordBridgeService(
    ILogger<TelegramDiscordBridgeService> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    WTelegramClientService wTelegramClientService,
    IDiscordGatewayService discordGatewayService,
    IHostApplicationLifetime lifetime
) : BackgroundService, ITelegramDiscordBridgeService
{
    private WTelegramClient? _client;
    private readonly ConcurrentDictionary<
        long,
        (List<TLMessage> Messages, Timer Timer)
    > _albumBuffers = new();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            _ = InitializeAsync(stoppingToken);
        });

        return Task.CompletedTask;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            _client = await wTelegramClientService.GetClientAsync(cancellationToken);
            _client.OnUpdates += OnUpdatesReceived;

            logger.LogInformation("TelegramDiscordBridgeService инициализирован");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка инициализации TelegramDiscordBridgeService");
        }
    }

    public async Task<OperationResult<List<TelegramDiscordBindingDto>>> GetBindingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<TelegramDiscordBindingDto>>.Bad(
            "Не удалось получить связи",
            []
        );

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var bindings = await dbContext
                .TelegramDiscordChannelBindings.AsNoTracking()
                .OrderBy(e => e.TelegramChannelId)
                .ThenBy(e => e.DiscordChannelId)
                .Select(e => new TelegramDiscordBindingDto
                {
                    Id = e.Id,
                    TelegramChannelId = e.TelegramChannelId,
                    DiscordChannelId = e.DiscordChannelId,
                    IsEnabled = e.IsEnabled,
                    LastError = e.LastError,
                    CreatedAtUtc = e.CreatedAtUtc,
                    UpdatedAtUtc = e.UpdatedAtUtc,
                })
                .ToListAsync(cancellationToken);

            result = OperationResult<List<TelegramDiscordBindingDto>>.Ok(
                "Связи получены",
                bindings
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения связей Telegram-Discord");
            result = OperationResult<List<TelegramDiscordBindingDto>>.Bad(ex.Message, []);
        }

        return result;
    }

    public async Task<OperationResult<TelegramDiscordBindingDto>> AddBindingAsync(
        TelegramDiscordBindingCreateRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<TelegramDiscordBindingDto>.Bad(
            "Не удалось создать связь",
            new TelegramDiscordBindingDto()
        );

        if (request.TelegramChannelId == 0 || request.DiscordChannelId == 0)
        {
            result = OperationResult<TelegramDiscordBindingDto>.Bad(
                "TelegramChannelId и DiscordChannelId должны быть заполнены",
                new TelegramDiscordBindingDto()
            );
        }
        else
        {
            try
            {
                var client = discordGatewayService.Client;
                if (client is null)
                {
                    client = await discordGatewayService.EnsureConnectedAsync(cancellationToken);
                }

                if (client is null)
                {
                    result = OperationResult<TelegramDiscordBindingDto>.Bad(
                        "Discord клиент недоступен",
                        new TelegramDiscordBindingDto()
                    );
                    return result;
                }

                try
                {
                    await client.GetChannelAsync(request.DiscordChannelId);
                }
                catch (DSharpPlus.Exceptions.NotFoundException)
                {
                    result = OperationResult<TelegramDiscordBindingDto>.Bad(
                        $"Discord канал {request.DiscordChannelId} не найден или нет доступа",
                        new TelegramDiscordBindingDto()
                    );
                    return result;
                }

                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var existing = await dbContext.TelegramDiscordChannelBindings.FirstOrDefaultAsync(
                    e =>
                        e.TelegramChannelId == request.TelegramChannelId
                        && e.DiscordChannelId == request.DiscordChannelId,
                    cancellationToken
                );

                if (existing is null)
                {
                    var now = DateTime.Now;
                    var entity = new TelegramDiscordChannelBinding
                    {
                        TelegramChannelId = request.TelegramChannelId,
                        DiscordChannelId = request.DiscordChannelId,
                        IsEnabled = true,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                    };

                    dbContext.TelegramDiscordChannelBindings.Add(entity);

                    var state = await dbContext.TelegramDiscordChannelStates.FirstOrDefaultAsync(
                        e => e.TelegramChannelId == request.TelegramChannelId,
                        cancellationToken
                    );

                    if (state is null)
                    {
                        dbContext.TelegramDiscordChannelStates.Add(
                            new TelegramDiscordChannelState
                            {
                                TelegramChannelId = request.TelegramChannelId,
                                LastProcessedMessageId = 0,
                                LastUpdatedUtc = now,
                            }
                        );
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    result = OperationResult<TelegramDiscordBindingDto>.Ok(
                        "Связь добавлена",
                        new TelegramDiscordBindingDto
                        {
                            Id = entity.Id,
                            TelegramChannelId = entity.TelegramChannelId,
                            DiscordChannelId = entity.DiscordChannelId,
                            IsEnabled = entity.IsEnabled,
                            LastError = entity.LastError,
                            CreatedAtUtc = entity.CreatedAtUtc,
                            UpdatedAtUtc = entity.UpdatedAtUtc,
                        }
                    );
                }
                else
                {
                    result = OperationResult<TelegramDiscordBindingDto>.Bad(
                        "Такая связь уже существует",
                        new TelegramDiscordBindingDto
                        {
                            Id = existing.Id,
                            TelegramChannelId = existing.TelegramChannelId,
                            DiscordChannelId = existing.DiscordChannelId,
                            IsEnabled = existing.IsEnabled,
                            LastError = existing.LastError,
                            CreatedAtUtc = existing.CreatedAtUtc,
                            UpdatedAtUtc = existing.UpdatedAtUtc,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка добавления связи Telegram-Discord");
                result = OperationResult<TelegramDiscordBindingDto>.Bad(
                    $"Ошибка добавления: {ex.Message}",
                    new TelegramDiscordBindingDto()
                );
            }
        }

        return result;
    }

    public async Task<OperationResult> DeleteBindingAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult.Bad("Не удалось удалить связь");

        if (id == Guid.Empty)
        {
            result = OperationResult.Bad("Id не может быть пустым");
        }
        else
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var entity = await dbContext.TelegramDiscordChannelBindings.FirstOrDefaultAsync(
                    e => e.Id == id,
                    cancellationToken
                );

                if (entity is not null)
                {
                    dbContext.TelegramDiscordChannelBindings.Remove(entity);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    result = OperationResult.Ok("Связь удалена");
                }
                else
                {
                    result = OperationResult.Bad("Связь не найдена");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка удаления связи Telegram-Discord {BindingId}", id);
                result = OperationResult.Bad($"Ошибка удаления: {ex.Message}");
            }
        }

        return result;
    }

    public async Task<OperationResult<TelegramDiscordBindingDto>> SetBindingEnabledAsync(
        Guid id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<TelegramDiscordBindingDto>.Bad(
            "Не удалось изменить состояние связи",
            new TelegramDiscordBindingDto()
        );

        if (id == Guid.Empty)
        {
            result = OperationResult<TelegramDiscordBindingDto>.Bad(
                "Id не может быть пустым",
                new TelegramDiscordBindingDto()
            );
        }
        else
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );
                var entity = await dbContext.TelegramDiscordChannelBindings.FirstOrDefaultAsync(
                    e => e.Id == id,
                    cancellationToken
                );

                if (entity is not null)
                {
                    entity.IsEnabled = isEnabled;
                    entity.UpdatedAtUtc = DateTime.Now;
                    if (isEnabled)
                    {
                        entity.LastError = null;
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    result = OperationResult<TelegramDiscordBindingDto>.Ok(
                        "Состояние связи обновлено",
                        new TelegramDiscordBindingDto
                        {
                            Id = entity.Id,
                            TelegramChannelId = entity.TelegramChannelId,
                            DiscordChannelId = entity.DiscordChannelId,
                            IsEnabled = entity.IsEnabled,
                            LastError = entity.LastError,
                            CreatedAtUtc = entity.CreatedAtUtc,
                            UpdatedAtUtc = entity.UpdatedAtUtc,
                        }
                    );
                }
                else
                {
                    result = OperationResult<TelegramDiscordBindingDto>.Bad(
                        "Связь не найдена",
                        new TelegramDiscordBindingDto()
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обновления связи Telegram-Discord {BindingId}", id);
                result = OperationResult<TelegramDiscordBindingDto>.Bad(
                    $"Ошибка обновления: {ex.Message}",
                    new TelegramDiscordBindingDto()
                );
            }
        }

        return result;
    }

    public async Task<OperationResult<List<TelegramDiscordChannelStateDto>>> GetStatesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<TelegramDiscordChannelStateDto>>.Bad(
            "Не удалось получить состояние каналов",
            []
        );

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );
            var states = await dbContext
                .TelegramDiscordChannelStates.AsNoTracking()
                .OrderBy(e => e.TelegramChannelId)
                .Select(e => new TelegramDiscordChannelStateDto
                {
                    TelegramChannelId = e.TelegramChannelId,
                    LastProcessedMessageId = e.LastProcessedMessageId,
                    LastUpdatedUtc = e.LastUpdatedUtc,
                })
                .ToListAsync(cancellationToken);

            result = OperationResult<List<TelegramDiscordChannelStateDto>>.Ok(
                "Состояния получены",
                states
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка чтения состояния Telegram-Discord bridge");
            result = OperationResult<List<TelegramDiscordChannelStateDto>>.Bad(ex.Message, []);
        }

        return result;
    }

    public async Task<OperationResult<List<TelegramChannelOptionDto>>> GetTelegramChannelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<TelegramChannelOptionDto>>.Bad(
            "Не удалось получить Telegram каналы",
            []
        );

        try
        {
            var client = _client;
            if (client is null)
            {
                client = await wTelegramClientService.GetClientAsync(cancellationToken);
            }

            var chats = await client.Messages_GetAllChats();
            var channels = chats
                .chats.Values.OfType<Channel>()
                .Select(channel => new TelegramChannelOptionDto
                {
                    Id = -1000000000000 - channel.id,
                    Title = string.IsNullOrWhiteSpace(channel.title)
                        ? $"channel-{channel.id}"
                        : channel.title,
                })
                .OrderBy(e => e.Title)
                .ThenBy(e => e.Id)
                .ToList();

            result = OperationResult<List<TelegramChannelOptionDto>>.Ok(
                "Telegram каналы получены",
                channels
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Telegram каналов для bridge");
            result = OperationResult<List<TelegramChannelOptionDto>>.Bad(
                $"Ошибка получения Telegram каналов: {ex.Message}",
                []
            );
        }

        return result;
    }

    public async Task<OperationResult<List<DiscordChannelOptionDto>>> GetDiscordChannelsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
            "Не удалось получить Discord каналы",
            []
        );

        try
        {
            var client = discordGatewayService.Client;
            if (client is null)
            {
                client = await discordGatewayService.EnsureConnectedAsync(cancellationToken);
            }

            if (client is not null)
            {
                var channels = client
                    .Guilds.Values.SelectMany(guild =>
                        guild
                            .Channels.Values.Where(channel =>
                            {
                                var channelType = channel.Type.ToString();
                                return channelType is "Text" or "Announcement";
                            })
                            .Select(channel => new DiscordChannelOptionDto
                            {
                                Id = channel.Id,
                                Name = channel.Name,
                                GuildId = guild.Id,
                                GuildName = guild.Name,
                            })
                    )
                    .OrderBy(e => e.GuildName)
                    .ThenBy(e => e.Name)
                    .ThenBy(e => e.Id)
                    .ToList();

                result = OperationResult<List<DiscordChannelOptionDto>>.Ok(
                    "Discord каналы получены",
                    channels
                );
            }
            else
            {
                result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                    "Discord клиент недоступен",
                    []
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка получения Discord каналов для bridge");
            result = OperationResult<List<DiscordChannelOptionDto>>.Bad(
                $"Ошибка получения Discord каналов: {ex.Message}",
                []
            );
        }

        return result;
    }

    private async Task OnUpdatesReceived(IObject updates)
    {
        try
        {
            if (updates is Updates updatesList)
            {
                foreach (var update in updatesList.UpdateList)
                {
                    if (update is UpdateNewChannelMessage channelMessage)
                    {
                        await HandleChannelMessageAsync(channelMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки обновлений Telegram для bridge");
        }
    }

    private async Task HandleChannelMessageAsync(UpdateNewChannelMessage update)
    {
        if (update.message is not TLMessage message)
        {
            return;
        }

        if (message.grouped_id != 0)
        {
            BufferAlbumMessage(message);
        }
        else
        {
            await ProcessSingleMessageAsync(message);
        }
    }

    private void BufferAlbumMessage(TLMessage message)
    {
        var groupedId = message.grouped_id;

        _albumBuffers.AddOrUpdate(
            groupedId,
            _ =>
            {
                var messages = new List<TLMessage> { message };
                var timer = new Timer(
                    async _ => await FlushAlbumAsync(groupedId),
                    null,
                    TimeSpan.FromSeconds(2),
                    Timeout.InfiniteTimeSpan
                );
                return (messages, timer);
            },
            (_, existing) =>
            {
                lock (existing.Messages)
                {
                    existing.Messages.Add(message);
                }

                existing.Timer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
                return existing;
            }
        );
    }

    private async Task FlushAlbumAsync(long groupedId)
    {
        if (!_albumBuffers.TryRemove(groupedId, out var buffer))
        {
            return;
        }

        await buffer.Timer.DisposeAsync();

        List<TLMessage> messages;
        lock (buffer.Messages)
        {
            messages = [.. buffer.Messages];
        }

        if (messages.Count == 0)
        {
            return;
        }

        var firstMessage = messages[0];
        var telegramChannelId = GetTelegramChannelId(firstMessage);
        if (telegramChannelId == 0)
        {
            return;
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetDiscordChannels = await GetTargetDiscordChannelsAsync(
                dbContext,
                telegramChannelId
            );
            if (targetDiscordChannels.Count == 0)
            {
                return;
            }

            var maxMessageId = messages.Max(m => m.ID);
            if (!await CheckAndUpdateStateAsync(dbContext, telegramChannelId, maxMessageId))
            {
                return;
            }

            var caption = ExtractPostText(firstMessage);

            var mediaFiles = await DownloadAlbumMediaAsync(messages);

            var isAllDelivered = true;
            foreach (var discordChannelId in targetDiscordChannels)
            {
                OperationResult sendResult;
                if (mediaFiles.Count > 0)
                {
                    sendResult = await discordGatewayService.SendFilesAsync(
                        discordChannelId,
                        mediaFiles,
                        caption
                    );
                }
                else
                {
                    sendResult = await discordGatewayService.SendMessageAsync(
                        discordChannelId,
                        caption
                    );
                }

                if (!sendResult.Success)
                {
                    isAllDelivered = false;
                    logger.LogWarning(
                        "Не удалось отправить Telegram альбом {GroupedId} в Discord канал {DiscordChannelId}: {Error}",
                        groupedId,
                        discordChannelId,
                        sendResult.Message
                    );
                    await HandleSendFailureAsync(
                        dbContext,
                        telegramChannelId,
                        discordChannelId,
                        sendResult.Message
                    );
                }
            }

            if (isAllDelivered)
            {
                var state = await dbContext.TelegramDiscordChannelStates.FirstOrDefaultAsync(e =>
                    e.TelegramChannelId == telegramChannelId
                );
                if (state is not null)
                {
                    state.LastProcessedMessageId = maxMessageId;
                    state.LastUpdatedUtc = DateTime.Now;
                    await dbContext.SaveChangesAsync();
                }
            }

            foreach (var (fileStream, _) in mediaFiles)
            {
                await fileStream.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка пересылки альбома {GroupedId} из Telegram канала {TelegramChannelId}",
                groupedId,
                telegramChannelId
            );
        }
    }

    private async Task ProcessSingleMessageAsync(TLMessage message)
    {
        var telegramChannelId = GetTelegramChannelId(message);
        if (telegramChannelId == 0)
        {
            return;
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetDiscordChannels = await GetTargetDiscordChannelsAsync(
                dbContext,
                telegramChannelId
            );
            if (targetDiscordChannels.Count == 0)
            {
                return;
            }

            if (!await CheckAndUpdateStateAsync(dbContext, telegramChannelId, message.ID))
            {
                return;
            }

            var caption = ExtractPostText(message);

            var mediaFile = await DownloadSingleMediaAsync(message);

            var isAllDelivered = true;
            foreach (var discordChannelId in targetDiscordChannels)
            {
                OperationResult sendResult;
                if (mediaFile is not null)
                {
                    sendResult = await discordGatewayService.SendFileAsync(
                        discordChannelId,
                        mediaFile.Value.Stream,
                        mediaFile.Value.FileName,
                        caption
                    );
                }
                else
                {
                    sendResult = await discordGatewayService.SendMessageAsync(
                        discordChannelId,
                        caption
                    );
                }

                if (!sendResult.Success)
                {
                    isAllDelivered = false;
                    logger.LogWarning(
                        "Не удалось отправить Telegram сообщение {MessageId} в Discord канал {DiscordChannelId}: {Error}",
                        message.ID,
                        discordChannelId,
                        sendResult.Message
                    );
                    await HandleSendFailureAsync(
                        dbContext,
                        telegramChannelId,
                        discordChannelId,
                        sendResult.Message
                    );
                }
            }

            if (isAllDelivered)
            {
                var state = await dbContext.TelegramDiscordChannelStates.FirstOrDefaultAsync(e =>
                    e.TelegramChannelId == telegramChannelId
                );
                if (state is not null)
                {
                    state.LastProcessedMessageId = message.ID;
                    state.LastUpdatedUtc = DateTime.Now;
                    await dbContext.SaveChangesAsync();
                }
            }

            if (mediaFile is not null)
            {
                await mediaFile.Value.Stream.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка пересылки сообщения {MessageId} из Telegram канала {TelegramChannelId}",
                message.ID,
                telegramChannelId
            );
        }
    }

    private static long GetTelegramChannelId(TLMessage message)
    {
        return message.Peer switch
        {
            PeerChannel peerChannel => -1000000000000 - peerChannel.channel_id,
            _ => 0L,
        };
    }

    private static string ExtractPostText(TLMessage message)
    {
        return string.IsNullOrWhiteSpace(message.message) ? string.Empty : message.message.Trim();
    }

    private async Task<List<ulong>> GetTargetDiscordChannelsAsync(
        AppDbContext dbContext,
        long telegramChannelId
    )
    {
        return await dbContext
            .TelegramDiscordChannelBindings.AsNoTracking()
            .Where(e => e.TelegramChannelId == telegramChannelId && e.IsEnabled)
            .Select(e => e.DiscordChannelId)
            .ToListAsync();
    }

    private async Task<bool> CheckAndUpdateStateAsync(
        AppDbContext dbContext,
        long telegramChannelId,
        int messageId
    )
    {
        var state = await dbContext.TelegramDiscordChannelStates.FirstOrDefaultAsync(e =>
            e.TelegramChannelId == telegramChannelId
        );

        if (state is null)
        {
            state = new TelegramDiscordChannelState
            {
                TelegramChannelId = telegramChannelId,
                LastProcessedMessageId = 0,
                LastUpdatedUtc = DateTime.Now,
            };
            dbContext.TelegramDiscordChannelStates.Add(state);
            await dbContext.SaveChangesAsync();
        }

        return messageId > state.LastProcessedMessageId;
    }

    private async Task HandleSendFailureAsync(
        AppDbContext dbContext,
        long telegramChannelId,
        ulong discordChannelId,
        string? errorMessage
    )
    {
        if (errorMessage?.Contains("не найден", StringComparison.OrdinalIgnoreCase) == true)
        {
            var binding = await dbContext.TelegramDiscordChannelBindings.FirstOrDefaultAsync(e =>
                e.TelegramChannelId == telegramChannelId && e.DiscordChannelId == discordChannelId
            );
            if (binding is not null)
            {
                binding.IsEnabled = false;
                binding.LastError = errorMessage;
                binding.UpdatedAtUtc = DateTime.Now;
                logger.LogWarning(
                    "Привязка TG:{TelegramChannelId} -> Discord:{DiscordChannelId} автоматически отключена: {Error}",
                    telegramChannelId,
                    discordChannelId,
                    errorMessage
                );
            }
        }
    }

    private async Task<List<(Stream Stream, string FileName)>> DownloadAlbumMediaAsync(
        List<TLMessage> messages
    )
    {
        var result = new List<(Stream Stream, string FileName)>();

        foreach (var message in messages)
        {
            var file = await DownloadSingleMediaAsync(message);
            if (file is not null)
            {
                result.Add(file.Value);
            }
        }

        return result;
    }

    private async Task<(Stream Stream, string FileName)?> DownloadSingleMediaAsync(
        TLMessage message
    )
    {
        if (message.media is null || _client is null)
        {
            return null;
        }

        try
        {
            return message.media switch
            {
                MessageMediaPhoto { photo: Photo photo } => await DownloadPhotoAsync(photo),
                MessageMediaDocument { document: Document document } => await DownloadDocumentAsync(
                    document
                ),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка скачивания медиа из сообщения {MessageId}", message.ID);
            return null;
        }
    }

    private async Task<(Stream Stream, string FileName)?> DownloadPhotoAsync(Photo photo)
    {
        if (_client is null)
        {
            return null;
        }

        var largestSize = photo
            .sizes.OfType<PhotoSize>()
            .OrderByDescending(s => (long)s.w * s.h)
            .FirstOrDefault();
        if (largestSize is null)
        {
            return null;
        }

        var stream = new MemoryStream();
        var fileType = await _client.DownloadFileAsync(photo, stream, largestSize);
        stream.Position = 0;

        var extension = GetExtensionForPhoto(fileType);
        var fileName = $"photo_{photo.id}{extension}";
        return (stream, fileName);
    }

    private async Task<(Stream Stream, string FileName)?> DownloadDocumentAsync(Document document)
    {
        if (_client is null)
        {
            return null;
        }

        var stream = new MemoryStream();
        await _client.DownloadFileAsync(document, stream);
        stream.Position = 0;

        var fileName = document
            .attributes.OfType<DocumentAttributeFilename>()
            .FirstOrDefault()
            ?.file_name;

        if (string.IsNullOrEmpty(fileName))
        {
            var extension = GetExtensionFromMimeType(document.mime_type);
            fileName = $"document_{document.id}{extension}";
        }
        else
        {
            var extension = GetExtensionFromMimeType(document.mime_type);
            if (
                !string.IsNullOrEmpty(extension)
                && !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            )
            {
                fileName += extension;
            }
        }

        return (stream, fileName);
    }

    public static string GetExtensionFromMimeType(string? mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" or "image/x-ms-bmp" => ".bmp",
            "image/tiff" or "image/x-tiff" => ".tiff",
            "image/svg+xml" => ".svg",
            "video/mp4" or "video/x-mp4" => ".mp4",
            "video/x-matroska" or "video/mkv" => ".mkv",
            "video/webm" => ".webm",
            "video/quicktime" or "video/mov" => ".mov",
            "video/x-msvideo" or "video/avi" => ".avi",
            "video/x-ms-wmv" => ".wmv",
            "video/mpeg" => ".mpeg",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/ogg" or "audio/vorbis" => ".ogg",
            "audio/aac" or "audio/x-aac" => ".aac",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/wav" or "audio/x-wav" or "audio/vnd.wave" => ".wav",
            "audio/opus" => ".opus",
            "audio/webm" => ".weba",
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "application/x-rar-compressed" or "application/vnd.rar" => ".rar",
            "application/x-7z-compressed" => ".7z",
            "application/gzip" or "application/x-gzip" => ".gz",
            "application/x-tar" => ".tar",
            "application/x-bzip2" => ".bz2",
            "text/plain" => ".txt",
            "text/html" => ".html",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            _ => "",
        };
    }

    public static string GetExtensionForPhoto(Storage_FileType fileType)
    {
        return fileType switch
        {
            Storage_FileType.jpeg => ".jpg",
            Storage_FileType.png => ".png",
            Storage_FileType.webp => ".webp",
            Storage_FileType.gif => ".gif",
            Storage_FileType.mov => ".mov",
            Storage_FileType.mp4 => ".mp4",
            _ => ".jpg",
        };
    }
}
