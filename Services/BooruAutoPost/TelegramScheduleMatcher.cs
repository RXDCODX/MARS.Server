using MARS.Server.Services.BooruAutoPost.Entities;

namespace MARS.Server.Services.BooruAutoPost;

public static class TelegramScheduleMatcher
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    public static int CountMatches(
        IReadOnlyList<TelegramScheduledMessageInfo> scheduledMessages,
        IReadOnlyList<DateTime> occurrences
    )
    {
        var result = 0;

        foreach (var message in scheduledMessages)
        {
            if (MatchesAny(message.ScheduledAtUtc, occurrences))
            {
                result++;
            }
        }

        return result;
    }

    public static DateTime? FindEarliestMatch(
        IEnumerable<TelegramScheduledMessageInfo> scheduledMessages,
        IReadOnlyList<DateTime> occurrences
    )
    {
        DateTime? result = null;

        foreach (var message in scheduledMessages)
        {
            if (
                MatchesAny(message.ScheduledAtUtc, occurrences)
                && (result is null || message.ScheduledAtUtc < result.Value)
            )
            {
                result = message.ScheduledAtUtc;
            }
        }

        return result;
    }

    public static List<DateTime> FindMissingOccurrences(
        IReadOnlyList<TelegramScheduledMessageInfo> scheduledMessages,
        IReadOnlyList<DateTime> occurrences
    )
    {
        var result = new List<DateTime>();

        foreach (var occurrence in occurrences)
        {
            if (!MatchesAnyOccurrence(scheduledMessages, occurrence))
            {
                result.Add(occurrence);
            }
        }

        return result;
    }

    public static List<TelegramScheduledMessageInfo> FindUnmatchedMessages(
        IReadOnlyList<TelegramScheduledMessageInfo> scheduledMessages,
        IReadOnlyList<DateTime> occurrences
    )
    {
        var result = new List<TelegramScheduledMessageInfo>();

        foreach (var message in scheduledMessages)
        {
            if (!MatchesAny(message.ScheduledAtUtc, occurrences))
            {
                result.Add(message);
            }
        }

        return result;
    }

    public static bool MatchesAny(DateTime messageDateUtc, IReadOnlyList<DateTime> occurrences)
    {
        var result = false;

        foreach (var occurrence in occurrences)
        {
            if ((messageDateUtc - occurrence).Duration() <= Tolerance)
            {
                result = true;
                break;
            }
        }

        return result;
    }

    private static bool MatchesAnyOccurrence(
        IReadOnlyList<TelegramScheduledMessageInfo> scheduledMessages,
        DateTime occurrence
    )
    {
        var result = false;

        foreach (var message in scheduledMessages)
        {
            if ((message.ScheduledAtUtc - occurrence).Duration() <= Tolerance)
            {
                result = true;
                break;
            }
        }

        return result;
    }
}
