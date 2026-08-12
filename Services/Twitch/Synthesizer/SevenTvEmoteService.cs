using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.SevenTv;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.Twitch.Synthesizer;

public sealed class SevenTvEmoteService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<SevenTvEmoteService> logger,
    ISevenTvApiService sevenTvApiService
) : BackgroundService, ISevenTvEmoteService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    private readonly Lock _emotesLock = new();

    private HashSet<string> _emoteNames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEmote(string word)
    {
        lock (_emotesLock)
        {
            return _emoteNames.Contains(word);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await LoadFromDatabaseAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("7TV emote service cancelled during initial load");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load 7TV emotes from database on startup");
        }

        using var timer = new PeriodicTimer(RefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshEmotesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
    }

    private async Task LoadFromDatabaseAsync(CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var names = await db.SevenTvEmotes.AsNoTracking().Select(e => e.Name).ToListAsync(ct);

        if (names.Count > 0)
        {
            lock (_emotesLock)
            {
                _emoteNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            }

            logger.LogInformation("Loaded {Count} 7TV emotes from database", names.Count);
        }
        else
        {
            await RefreshEmotesAsync(ct);
        }
    }

    private async Task RefreshEmotesAsync(CancellationToken ct)
    {
        try
        {
            var fetchedEmotes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var sevenTvUser = await sevenTvApiService.GetUserAsync(TwitchExstension.SevenTvUserId);

            if (sevenTvUser is { emote_sets: { Length: > 0 } })
            {
                foreach (var emoteSet in sevenTvUser.emote_sets)
                {
                    if (emoteSet is { id: not null })
                    {
                        var emoteSetEmojis = await sevenTvApiService.GetEmoteSetAsync(emoteSet.id);

                        if (emoteSetEmojis is { emotes: { Length: > 0 } })
                        {
                            foreach (var emote in emoteSetEmojis.emotes)
                            {
                                if (emote is { name: not null })
                                {
                                    fetchedEmotes.Add(emote.name);
                                }
                            }
                        }
                    }
                }
            }

            if (fetchedEmotes.Count > 0)
            {
                var now = DateTime.Now;
                var emoteEntities = fetchedEmotes
                    .Select(name => new Entitys.SevenTvEmote { Name = name, LoadedAt = now })
                    .ToList();

                await using var db = await dbContextFactory.CreateDbContextAsync(ct);

                await db.SevenTvEmotes.ExecuteDeleteAsync(ct);
                db.SevenTvEmotes.AddRange(emoteEntities);
                await db.SaveChangesAsync(ct);

                lock (_emotesLock)
                {
                    _emoteNames = fetchedEmotes;
                }

                logger.LogInformation(
                    "Refreshed 7TV emotes: {Count} emotes loaded from API",
                    fetchedEmotes.Count
                );
            }
            else
            {
                logger.LogWarning(
                    "7TV emote list is empty for user {SevenTvUserId}. TTS emote filtering is disabled.",
                    TwitchExstension.SevenTvUserId
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh 7TV emotes. Will retry on next timer tick.");
        }
    }
}
