using MARS.Server.Services.SoundRequest.Entitys;

namespace MARS.Server.Services.SoundRequest;

public class SoundRequestHistoryService
{
    private readonly CancellationToken _cancellationToken;
    private static readonly LinkedList<BaseTrackInfo> History = [];
    private readonly IDbContextFactory<AppDbContext> _factory;
    private const int HistoryCount = 20;

    private static readonly Lock RemoveLocker = new();
    private static readonly Lock AddLocker = new();

    public SoundRequestHistoryService(
        IDbContextFactory<AppDbContext> factory,
        IHostApplicationLifetime lifetime,
        SoundRequestSignalREvents events
    )
    {
        _factory = factory;
        _cancellationToken = lifetime.ApplicationStopping;
        events.StartedEvent += SoundRequestHubOnStartedEvent;
    }

    private Task SoundRequestHubOnStartedEvent(BaseTrackInfo trackinfo)
    {
        return Task.Factory.StartNew(() => AddTrackToHistory(trackinfo), _cancellationToken);
    }

    private Task AddTrackToHistory(BaseTrackInfo info)
    {
        lock (RemoveLocker)
        {
            while (History.Count > HistoryCount - 1)
            {
                History.RemoveLast();
            }
        }

        lock (AddLocker)
        {
            History.AddFirst(info);
        }

        return Task.CompletedTask;
    }

    public async Task<BaseTrackInfo[]> GetLastPlayedTracks(int count = 20)
    {
        // Если в истории недостаточно треков, дополняем из БД
        await using var dbContext = await _factory.CreateDbContextAsync(_cancellationToken);
        var dbTracks = await dbContext
            .SoundRequestBaseTrackInfos.Where(e => e.LastTimePlays > DateTime.UnixEpoch)
            .OrderByDescending(e => e.LastTimePlays)
            .Take(count)
            .ToArrayAsync(cancellationToken: _cancellationToken);

        return dbTracks;
    }
}
