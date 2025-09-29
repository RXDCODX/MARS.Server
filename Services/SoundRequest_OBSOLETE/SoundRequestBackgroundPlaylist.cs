using System.Collections.Concurrent;
using MARS.Server.Services.SoundRequest_OBSOLETE.Entitys;

namespace MARS.Server.Services.SoundRequest_OBSOLETE;

/// <summary>
/// Represents a playlist for background sound requests, managing the queue and playback of tracks.
/// </summary>
public class SoundRequestBackgroundPlaylist(
    IDbContextFactory<AppDbContext> factory,
    IHostApplicationLifetime lifetime
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;
    public readonly ConcurrentQueue<BaseTrackInfo> BackGroundPlaylist = [];

    public readonly Timer Timer = new(TimeSpan.FromHours(6));

    private const string UserPublicId = "jx0yagyvdr5nkfxw98dc8f9pmr";
    private const string UserId = "206686091";
    private const string PlaylistId = "lk.a499c4f9-13d1-49e5-9910-0d4d70b7a4cd";

    public async Task AddTrackAsync(BaseTrackInfo info)
    {
        BackGroundPlaylist.Enqueue(info);
        await using var dbContext = await factory.CreateDbContextAsync(_cancellationToken);
        var isExists = dbContext.SoundRequestBackgroundTracks.Any(e =>
            e.BaseTrackInfo.Url == info.Url
        );

        if (isExists)
        {
            return;
        }

        dbContext.SoundRequestBackgroundTracks.Add(
            new SoundRequestBackgroundTrackId() { BaseTrackInfo = info }
        );
        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
