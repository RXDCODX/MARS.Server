using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Telegram.DiscordBridge.Entities;
using MARS.Server.Services.Telegram.DiscordBridge.Entitys;
using MARS.Server.Services.Telegram.WTelegram;
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
                    var now = DateTime.UtcNow;
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
                    entity.UpdatedAtUtc = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    result = OperationResult<TelegramDiscordBindingDto>.Ok(
                        "Состояние связи обновлено",
                        new TelegramDiscordBindingDto
                        {
                            Id = entity.Id,
                            TelegramChannelId = entity.TelegramChannelId,
                            DiscordChannelId = entity.DiscordChannelId,
                            IsEnabled = entity.IsEnabled,
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
                        await ProcessChannelMessageAsync(channelMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка обработки обновлений Telegram для bridge");
        }
    }

    private async Task ProcessChannelMessageAsync(UpdateNewChannelMessage update)
    {
        if (update.message is not TLMessage message)
        {
            return;
        }

        var telegramChannelId = message.Peer switch
        {
            PeerChannel peerChannel => -1000000000000 - peerChannel.channel_id,
            _ => 0L,
        };

        if (telegramChannelId == 0)
        {
            return;
        }

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var targetDiscordChannels = await dbContext
                .TelegramDiscordChannelBindings.AsNoTracking()
                .Where(e => e.TelegramChannelId == telegramChannelId && e.IsEnabled)
                .Select(e => e.DiscordChannelId)
                .ToListAsync();

            if (targetDiscordChannels.Count == 0)
            {
                return;
            }

            var state = await dbContext.TelegramDiscordChannelStates.FirstOrDefaultAsync(e =>
                e.TelegramChannelId == telegramChannelId
            );

            if (state is null)
            {
                state = new TelegramDiscordChannelState
                {
                    TelegramChannelId = telegramChannelId,
                    LastProcessedMessageId = 0,
                    LastUpdatedUtc = DateTime.UtcNow,
                };
                dbContext.TelegramDiscordChannelStates.Add(state);
                await dbContext.SaveChangesAsync();
            }

            if (message.ID <= state.LastProcessedMessageId)
            {
                return;
            }

            var payload = BuildDiscordMessagePayload(message, telegramChannelId);

            var isAllDelivered = true;
            foreach (var discordChannelId in targetDiscordChannels)
            {
                var sendResult = await discordGatewayService.SendMessageAsync(
                    discordChannelId,
                    payload
                );
                if (!sendResult.Success)
                {
                    isAllDelivered = false;
                    logger.LogWarning(
                        "Не удалось отправить Telegram сообщение {MessageId} в Discord канал {DiscordChannelId}: {Error}",
                        message.ID,
                        discordChannelId,
                        sendResult.Message
                    );
                }
            }

            if (isAllDelivered)
            {
                state.LastProcessedMessageId = message.ID;
                state.LastUpdatedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка пересылки сообщения {MessageId} из Telegram канала {TelegramChannelId} в Discord",
                message.ID,
                telegramChannelId
            );
        }
    }

    private static string BuildDiscordMessagePayload(TLMessage message, long telegramChannelId)
    {
        var sourcePart =
            $"[TG:{telegramChannelId}] msg:{message.ID} time:{message.Date:yyyy-MM-dd HH:mm:ss} UTC";
        var text = string.IsNullOrWhiteSpace(message.message)
            ? "(сообщение без текста)"
            : message.message.Trim();

        var mediaPart = message.media is null
            ? string.Empty
            : $"\n[media: {message.media.GetType().Name}]";

        return $"{sourcePart}\n{text}{mediaPart}";
    }
}
