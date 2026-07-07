using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.CommandExecutor.Adapters;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.Telegram.BotService.Entitys;
using MARS.Server.Services.Telegram.ClipboardCopy;
using MARS.Server.Services.Telegram.GooglePhotos;
using MARS.Server.Services.Telegram.WTelegram;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.Telegram.BotService;

public class UpdateHandler : IUpdateHandler
{
    public delegate Task TelegramUpdateDelegate(ITelegramBotClient client, Update update);
    public event TelegramUpdateDelegate TelegramUpdate = (client, update) => Task.CompletedTask;

    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly TelegramConfiguration _options;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        IOptions<TelegramConfiguration> options,
        PyroAlertsHandler pyroAlertsHandler,
        RandomMemHandler randomMemHandler,
        IHostApplicationLifetime applicationLifetime,
        IDbContextFactory<AppDbContext> dbContextFactory,
        TelegramCommandService telegramCommandService,
        TelegramClipboardCopyService telegramClipboardCopyService,
        TelegramGooglePhotosService telegramGooglePhotosService,
        IServiceProvider serviceProvider
    )
    {
        _botClient = botClient;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _options = options.Value;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            TelegramUpdate += pyroAlertsHandler.HandAlert;
            TelegramUpdate += randomMemHandler.HandMessage;
            TelegramUpdate += telegramCommandService.HandMessage;
            TelegramUpdate += telegramCommandService.HandInlineQuery;
            TelegramUpdate += telegramCommandService.HandChosenInlineResult;
            TelegramUpdate += telegramClipboardCopyService.HandMessage;
            TelegramUpdate += telegramGooglePhotosService.HandMessage;

            // Получаем Singleton WTelegramClientService из корневого провайдера
            var wTelegramClientService =
                serviceProvider.GetRequiredService<WTelegramClientService>();
            TelegramUpdate += wTelegramClientService.HandleUpdate;
        });

        applicationLifetime.ApplicationStopped.Register(() =>
        {
            botClient.SendMessage(TelegramExstension.Rxdcodx, "Приложение остановленно!");
        });
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient _,
        Update update,
        CancellationToken cancellationToken
    )
    {
        if (update != null)
        {
            try
            {
                await UpdateOffset(update.Id, cancellationToken);

                await ResendMessage(update);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }

            Task handler = update switch
            {
                //{ ChannelPost: {} channelPost } => BotOnChannelPost(channelPost, cancellationToken),
                { InlineQuery: { } inlineQuery } => BotOnInlineQueryReceived(
                    inlineQuery,
                    cancellationToken
                ),
                _ => UnknownUpdateHandlerAsync(update, cancellationToken),
            };

            await handler;
            await TelegramUpdate.Invoke(_, update);
        }
    }

    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken
    )
    {
        _logger.LogException(exception);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception != null)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException =>
                    $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString(),
            };

            _logger.LogInformation("HandleError: {ErrorMessage}", errorMessage);

            // Cooldown in case of network connection error
            if (exception is RequestException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task ResendMessage(Update update)
    {
        if (update != null && _options.AdminIdsArray != null)
        {
            foreach (var id in _options.AdminIdsArray)
            {
                switch (update.Type)
                {
                    case UpdateType.Message:
                        var messageId = update.Message!.MessageId;
                        var chatId = update.Message.Chat.Id;

                        if (update.Message.HasProtectedContent != true)
                        {
                            try
                            {
                                await _botClient.ForwardMessage(id, chatId, messageId);
                            }
                            catch (ApiRequestException ex)
                                when (ex.ErrorCode == 400
                                    && ex.Message.Contains(
                                        "message to forward not found",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Не удалось переслать сообщение {MessageId} из чата {ChatId}",
                                    messageId,
                                    chatId
                                );
                            }
                        }

                        break;
                    case UpdateType.ChannelPost:
                        messageId = update.ChannelPost!.MessageId;
                        chatId = update.ChannelPost.Chat.Id;

                        if (update.ChannelPost.HasProtectedContent != true)
                        {
                            try
                            {
                                await _botClient.ForwardMessage(id, chatId, messageId);
                            }
                            catch (ApiRequestException ex)
                                when (ex.ErrorCode == 400
                                    && ex.Message.Contains(
                                        "message to forward not found",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Не удалось переслать сообщение {MessageId} из канала {ChatId}",
                                    messageId,
                                    chatId
                                );
                            }
                        }

                        //if (_environment.IsDevelopment())
                        //    _logger.LogCritical(update.ChannelPost.Text);
                        break;
                }
            }
        }
    }

    #region Inline Mode

    private async Task BotOnInlineQueryReceived(
        InlineQuery inlineQuery,
        CancellationToken cancellationToken
    )
    {
        if (inlineQuery != null)
        {
            _logger.LogInformation(
                "Получен inline query от пользователя {InlineQueryFromId}: {InlineQuery}",
                inlineQuery.From.Id,
                inlineQuery.Query
            );
        }

        await Task.CompletedTask;
    }

    #endregion

#pragma warning disable IDE0060 // Remove unused parameter
    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    private async Task UpdateOffset(int updateId, CancellationToken cancellationToken)
    {
        if (updateId > 0)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var offset = await dbContext.TelegramUpdateReceiverOffset.SingleOrDefaultAsync(
                cancellationToken: cancellationToken
            );

            if (offset is not null)
            {
                if (updateId != offset.Offset + 1)
                {
                    offset.Offset = updateId;
                }
                else
                {
                    offset.Offset += 1;
                }
            }
            else
            {
                var obset = new TelegramUpdateReceiverOffset
                {
                    Offset = updateId,
                    Id = Guid.NewGuid(),
                };

                await dbContext.TelegramUpdateReceiverOffset.AddAsync(obset, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
