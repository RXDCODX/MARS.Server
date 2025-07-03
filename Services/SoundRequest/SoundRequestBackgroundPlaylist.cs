using System.Collections.Concurrent;
using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Services.SoundRequest;

public class SoundRequestBackgroundPlaylist
{
    public readonly ConcurrentQueue<BaseTrackInfo> BackGroundPlaylist;

    public SoundRequestBackgroundPlaylist(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        using var dbContext = factory.CreateDbContext();
        var listTracks = dbContext
            .SoundRequestBackgroundTracks.Include(e => e.BaseTrackInfo)
            .AsNoTrackingWithIdentityResolution()
            .AsEnumerable();
        BackGroundPlaylist = new ConcurrentQueue<BaseTrackInfo>(
            listTracks.Select(e => e.BaseTrackInfo)
        );
    }

    private readonly IDbContextFactory<AppDbContext> _factory;

    public Task AddTrack(BaseTrackInfo info)
    {
        BackGroundPlaylist.Enqueue(info);
        using var dbContext = _factory.CreateDbContext();
        var isExists = dbContext.SoundRequestBackgroundTracks.Any(e =>
            e.BaseTrackInfo.Url == info.Url
        );

        if (isExists)
        {
            return Task.CompletedTask;
        }

        dbContext.SoundRequestBackgroundTracks.Add(
            new SoundRequestBackgroundTrackId() { BaseTrackInfo = info }
        );
        dbContext.SaveChanges();

        return Task.CompletedTask;
    }
}
