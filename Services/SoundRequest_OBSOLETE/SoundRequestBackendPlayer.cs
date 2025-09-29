using MARS.Server.Services.SoundRequest_OBSOLETE.Entitys;

namespace MARS.Server.Services.SoundRequest_OBSOLETE;

/// <summary>
/// Handles background processing and playback of sound requests.
/// </summary>
public class SoundRequestBackendPlayer : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SoundRequestSignalREvents _events;
    private readonly IHubContext<SoundRequestHub, ISoundRequestHub> _hub;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<SoundRequestBackendPlayer> _logger;

    private readonly SoundRequestUserQueue _userQueue;
    private readonly SoundRequestBackgroundPlaylist _backgroundPlaylist;

    private ISoundRequestHub ClientGroup => _hub.Clients.Group("client");
    public bool IsActive { get; private set; }
    private volatile PlayerState _playerState;
    private CancellationTokenSource _playbackCancellationTokenSource;
    private Task _currentPlaybackTask;
    private readonly CancellationToken _cancellationToken;

    public PlayerState PlayerState
    {
        get { return _playerState; }
        set { _playerState = value; }
    }

    public SoundRequestBackendPlayer(
        IHostApplicationLifetime lifetime,
        SoundRequestSignalREvents events,
        IDbContextFactory<AppDbContext> factory,
        SoundRequestUserQueue userQueue,
        SoundRequestBackgroundPlaylist backgroundPlaylist,
        ILogger<SoundRequestBackendPlayer> logger,
        IHubContext<SoundRequestHub, ISoundRequestHub> hub
    )
    {
        _lifetime = lifetime;
        _cancellationToken = lifetime.ApplicationStopping;
        _events = events;
        _dbContextFactory = factory;
        _userQueue = userQueue;
        _backgroundPlaylist = backgroundPlaylist;
        _logger = logger;
        _hub = hub;
        _currentPlaybackTask = Task.CompletedTask;
        _playbackCancellationTokenSource = new CancellationTokenSource();

        using var dbContext = factory.CreateDbContext();
        var oldState = dbContext
            .SoundRequestPlayerState.AsEnumerable()
            .SingleOrDefault(new PlayerState());
        oldState.IsPaused = true;
        _playerState = oldState;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
        {
            IsActive = true;
            _events.ErrorEvent += HubOnErrorEvent;
            _events.EndedEvent += HubOnEndedEvent;
            _events.StartedEvent += HubOnStartedEvent;
            _events.StartedEvent += UpdateTrackLaunchTime;

            // Start playback loop if there's a current track

            PlayerState.EntityChanged += PlayerStateOnEntityChanged;
            if (PlayerState is { CurrentTrack: not null, IsStoped: false })
            {
                _currentPlaybackTask = ProcessPlaybackAsync(_playbackCancellationTokenSource.Token);
            }
        });

        return Task.CompletedTask;
    }

    private async Task UpdateTrackLaunchTime(BaseTrackInfo trackinfo)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
            _cancellationToken
        );
        trackinfo.LastTimePlays = DateTime.Now;
        dbContext.SoundRequestBaseTrackInfos.Update(trackinfo);
        await dbContext.SaveChangesAsync(_cancellationToken);
    }

    private async Task PlayerStateOnEntityChanged(object? sender, EventArgs eventargs)
    {
        await Task.Factory.StartNew(
            async () =>
            {
                var value = (PlayerState)sender!;
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                    _cancellationToken
                );
                dbContext.SoundRequestPlayerState.AddOrUpdate(value);
                await dbContext.SaveChangesAsync(true, _cancellationToken);
                await ClientGroup.PlayerStateChange(value).ConfigureAwait(false);
                _playerState = value;
            },
            _cancellationToken
        );
    }

    private Task HubOnStartedEvent(BaseTrackInfo trackInfo)
    {
        _logger.LogInformation("Track started playing: {TrackTitle}", trackInfo.Title);
        PlayerState.UpdatePlayerState(
            (state) =>
            {
                state.CurrentTrack = trackInfo;
                state.IsStoped = false;
                state.IsPaused = false;
            }
        );

        return Task.CompletedTask;
    }

    public async Task SkipTrack()
    {
        if (PlayerState.CurrentTrack != null)
        {
            _logger.LogInformation("Track skipped: {TrackTitle}", PlayerState.CurrentTrack.Title);
            await PlayNextTrackAsync();
        }
    }

    private async Task HubOnEndedEvent(BaseTrackInfo trackInfo)
    {
        _logger.LogInformation("Track ended: {TrackTitle}", trackInfo.Title);
        await PlayNextTrackAsync();
    }

    private async Task HubOnErrorEvent(BaseTrackInfo trackInfo)
    {
        _logger.LogError("Error playing track: {TrackTitle}", trackInfo.Title);
        await PlayNextTrackAsync();
    }

    private async Task ProcessPlaybackAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsActive)
        {
            try
            {
                if (PlayerState.CurrentTrack == null || PlayerState.IsStoped)
                {
                    await GetNextTrackAsync();
                    continue;
                }

                if (PlayerState.IsPaused)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                // Simulate playback progress (in a real implementation, this would come from the player)
                await Task.Delay(1000, cancellationToken);

                if (PlayerState.CurrentTrackDuration.HasValue)
                {
                    // Update progress (this would be handled by the actual player in production)
                    PlayerState.UpdatePlayerState(state =>
                        state.CurrentTrackDuration =
                            PlayerState.CurrentTrackDuration.Value.Subtract(TimeSpan.FromSeconds(1))
                    );
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback processing was canceled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in playback processing");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task GetNextTrackAsync()
    {
        try
        {
            // Check user queue first
            var userQueue = await _userQueue.GetQueueAsync();
            if (userQueue.Count > 0)
            {
                var nextTrack = userQueue.OrderBy(t => t.Order).First();
                PlayerState.UpdatePlayerState(state =>
                {
                    state.CurrentTrack = nextTrack.RequestedTrack;
                    state.NextTrack =
                        userQueue.Count > 1
                            ? userQueue[1].RequestedTrack
                            : GetNextBackgroundTrack();
                    state.CurrentTrackDuration = nextTrack.RequestedTrack.Duration;
                    state.IsStoped = false;
                    state.IsPaused = false;
                });

                // Remove the track from queue after setting it as current
                await _userQueue.RemoveFromQueueAsync(nextTrack.Id);
                return;
            }

            // If no user tracks, get from background playlist
            var backgroundTrack = GetNextBackgroundTrack();
            if (backgroundTrack != null)
            {
                PlayerState.UpdatePlayerState(state =>
                {
                    state.CurrentTrack = backgroundTrack;
                    state.NextTrack = GetNextBackgroundTrack();
                    state.CurrentTrackDuration = backgroundTrack.Duration;
                    state.IsStoped = false;
                    state.IsPaused = false;
                });
                return;
            }

            // No tracks available
            PlayerState.UpdatePlayerState(state =>
            {
                state.CurrentTrack = null;
                state.NextTrack = null;
                state.CurrentTrackDuration = null;
                state.IsStoped = true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next track");
        }
    }

    private BaseTrackInfo? GetNextBackgroundTrack()
    {
        if (_backgroundPlaylist.BackGroundPlaylist.TryDequeue(out var track))
        {
            // Re-add to end of queue for continuous playback
            _backgroundPlaylist.BackGroundPlaylist.Enqueue(track);
            return track;
        }
        return null;
    }

    public async Task PlayNextTrackAsync()
    {
        if (!IsActive)
        {
            return;
        }

        await _playbackCancellationTokenSource.CancelAsync();
        await (_currentPlaybackTask);

        _playbackCancellationTokenSource = new CancellationTokenSource();
        await GetNextTrackAsync();

        if (PlayerState.CurrentTrack != null)
        {
            _currentPlaybackTask = ProcessPlaybackAsync(_playbackCancellationTokenSource.Token);
        }
    }

    public void MutePlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState is { CurrentTrack: not null, IsMuted: false })
        {
            PlayerState.UpdatePlayerState(state => state.IsMuted = true);
        }
    }

    public void UnmutePlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState is { CurrentTrack: not null, IsMuted: true })
        {
            PlayerState.UpdatePlayerState(state => state.IsMuted = false);
        }
    }

    public void UnStopPlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState.CurrentTrack != null)
        {
            PlayerState.UpdatePlayerState(state => state.IsStoped = false);
        }
    }

    public void StopPlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState.CurrentTrack != null)
        {
            _playbackCancellationTokenSource.Cancel();
            PlayerState.UpdatePlayerState(state =>
            {
                state.CurrentTrack = null;
                state.NextTrack = null;
                state.CurrentTrackDuration = null;
                state.IsStoped = true;
                state.IsPaused = false;
            });
        }
    }

    public void PausePlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState is { CurrentTrack: not null, IsPaused: false, IsStoped: false })
        {
            PlayerState.UpdatePlayerState(state => state.IsPaused = true);
        }
    }

    public void ResumePlayer()
    {
        if (!IsActive)
        {
            return;
        }

        if (PlayerState is { CurrentTrack: not null, IsPaused: true })
        {
            PlayerState.UpdatePlayerState(state => state.IsPaused = false);
        }
    }

    public void SetVolume(int volume)
    {
        if (!IsActive || volume < 0 || volume > 100)
        {
            return;
        }

        PlayerState.UpdatePlayerState(state => state.Volume = volume);
    }
}
