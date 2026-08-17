using Cronos;

namespace MARS.Server.Services.BooruShared;

public static class BooruValidationHelper
{
    public static string? ValidateAndParseDiscordChannelId(
        string raw,
        out ulong parsed,
        string fieldName = "DiscordChannelId"
    )
    {
        string? result;

        if (string.IsNullOrWhiteSpace(raw))
        {
            parsed = 0;
            result = $"{fieldName} обязателен";
        }
        else if (!ulong.TryParse(raw, out parsed) || parsed == 0)
        {
            result = $"Некорректный {fieldName}: '{raw}'";
        }
        else
        {
            result = null;
        }

        return result;
    }

    public static string? ValidateAndParseTelegramChannelId(
        string raw,
        out long parsed,
        string fieldName = "TelegramChannelId"
    )
    {
        string? result;

        if (string.IsNullOrWhiteSpace(raw))
        {
            parsed = 0;
            result = $"{fieldName} обязателен";
        }
        else if (!long.TryParse(raw, out parsed) || parsed == 0)
        {
            result = $"Некорректный {fieldName}: '{raw}'";
        }
        else
        {
            result = null;
        }

        return result;
    }

    public static string? ValidateCronExpression(string? cronExpression)
    {
        string? result;

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            result = "CRON выражение обязательно";
        }
        else
        {
            try
            {
                CronExpression.Parse(cronExpression);
                result = null;
            }
            catch (CronFormatException)
            {
                result = "Некорректное CRON выражение";
            }
        }

        return result;
    }
}
