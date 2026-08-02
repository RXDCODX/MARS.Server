using System;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.Exstensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services._365Genius;

public sealed class SiteUnavailableNotifier(
    ITelegramBotClient botClient,
    IOptions<TelegramConfiguration> config,
    ILogger<SiteUnavailableNotifier> logger
)
{
    private readonly long[] _adminsArray = config.Value.AdminIdsArray ?? [];

    public async Task NotifyAsync(Uri site, Exception error, CancellationToken cancellationToken)
    {
        var message = $"""
            <b>Сайт {site} недоступен!</b>

            Ошибка: {error.Message}
            """;

        foreach (var adminId in _adminsArray)
        {
            try
            {
                await botClient.SendMessage(
                    adminId,
                    message,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception e)
            {
                logger.LogException(e);
            }
        }
    }
}
