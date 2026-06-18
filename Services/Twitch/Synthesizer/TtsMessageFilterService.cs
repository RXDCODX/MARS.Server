using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsMessageFilterService : ITtsMessageFilterService
{
    private readonly IDbContextFactory<AppDbContext>? _dbContextFactory;
    private readonly ILogger<TtsMessageFilterService>? _logger;
    private readonly TimeSpan _dedupWindow;

    private readonly ConcurrentDictionary<string, DateTime> _recentMessages = new(
        StringComparer.OrdinalIgnoreCase
    );

    private const int CleanupThreshold = 100;
    private int _messagesSinceCleanup;

    public bool IsFilterEnabled { get; set; } = true;

    public TtsMessageFilterService(
        IDbContextFactory<AppDbContext>? dbContextFactory = null,
        ILogger<TtsMessageFilterService>? logger = null,
        TimeSpan? dedupWindow = null
    )
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _dedupWindow = dedupWindow ?? TimeSpan.FromSeconds(30);
    }

    public async Task LoadStateAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextFactory is null)
        {
            return;
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var value = await db
                .RootState.AsNoTracking()
                .Where(e => e.Name == RootStateKeys.TtsFilterEnabled)
                .Select(e => e.Value)
                .FirstOrDefaultAsync(cancellationToken);

            if (bool.TryParse(value, out var isEnabled))
            {
                IsFilterEnabled = isEnabled;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load TtsFilterEnabled state from DB, using default.");
        }
    }

    public OperationResult<string> FilterMessage(string message, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return OperationResult<string>.Bad("Сообщение пустое");
        }

        if (!IsFilterEnabled)
        {
            return OperationResult<string>.Ok("Фильтр отключён", message);
        }

        var collapsed = CollapseRepetitions(message);

        var normalized = collapsed.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        if (_messagesSinceCleanup >= CleanupThreshold)
        {
            Cleanup(now);
        }

        if (
            _recentMessages.TryGetValue(normalized, out var firstSeen)
            && now - firstSeen < _dedupWindow
        )
        {
            return OperationResult<string>.Bad("Обнаружен дубликат сообщения");
        }

        _recentMessages[normalized] = now;
        _messagesSinceCleanup++;

        return OperationResult<string>.Ok("Сообщение обработано", collapsed);
    }

    private void Cleanup(DateTime now)
    {
        var cutoff = now - _dedupWindow;

        foreach (var kvp in _recentMessages)
        {
            if (kvp.Value < cutoff)
            {
                _recentMessages.TryRemove(kvp.Key, out _);
            }
        }

        _messagesSinceCleanup = 0;
    }

    internal static string CollapseRepetitions(string message)
    {
        var words = message.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        if (words.Length < 2)
        {
            return message;
        }

        var afterWordCollapse = CollapseWordRepetitions(words);

        var result = CollapsePhraseRepetitions(afterWordCollapse);

        return result;
    }

    private static List<string> CollapseWordRepetitions(string[] words)
    {
        var result = new List<string>(words.Length);

        var i = 0;
        while (i < words.Length)
        {
            var current = words[i];
            var count = 1;

            while (
                i + count < words.Length
                && string.Equals(
                    words[i + count],
                    current,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                count++;
            }

            if (count >= 3)
            {
                result.Add(current);
            }
            else
            {
                for (var j = 0; j < count; j++)
                {
                    result.Add(current);
                }
            }

            i += count;
        }

        return result;
    }

    private static string CollapsePhraseRepetitions(List<string> words)
    {
        if (words.Count < 2)
        {
            return string.Join(' ', words);
        }

        var n = words.Count;

        for (var repeats = n / 2; repeats >= 2; repeats--)
        {
            if (n % repeats != 0)
            {
                continue;
            }

            var phraseLen = n / repeats;

            var isRepeated = true;
            for (var i = phraseLen; i < n; i++)
            {
                if (
                    !string.Equals(
                        words[i],
                        words[i % phraseLen],
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    isRepeated = false;
                    break;
                }
            }

            if (isRepeated)
            {
                return string.Join(' ', words.Take(phraseLen));
            }
        }

        return string.Join(' ', words);
    }
}
