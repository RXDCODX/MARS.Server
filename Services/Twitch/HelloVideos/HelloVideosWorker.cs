using MARS.Server.Services.Twitch.SoundBarService;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.HelloVideos;

public class HelloVideoWorker
{
    private readonly CancellationToken _token;
    private readonly List<string> _users = new();
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<HelloVideoWorker> _logger;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly SoundBarFactory _soundBarFactory;

    public HelloVideoWorker(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<HelloVideoWorker> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        ITwitchClient client,
        SoundBarFactory soundBarFactory
    )
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _hubContext = hubContext;
        this._soundBarFactory = soundBarFactory;
        _token = hostApplicationLifetime.ApplicationStopping;

        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += OnMessageReceived;
        });
    }

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (args.ChatMessage.Channel != TwitchExstension.Channel)
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                try
                {
                    var now = DateTimeOffset.Now;
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                        _token
                    );
                    var user = await dbContext.FumoUsers.FindAsync(args.ChatMessage.UserId, _token);
                    var notifUser = await dbContext
                        .HelloVideosUsers.Include(e => e.MediaInfo)
                        .FirstOrDefaultAsync(e => e.TwitchId == args.ChatMessage.UserId, _token);

                    if (now.DayOfWeek == DayOfWeek.Friday && user != null)
                    {
                        return;
                    }

                    if (_users.Contains(args.ChatMessage.Id))
                    {
                        return;
                    }

                    if (notifUser != null)
                    {
                        if (notifUser.LastTimeNotif.Day != now.Day)
                        {
                            notifUser.LastTimeNotif = now;
                            await dbContext.SaveChangesAsync(_token);

                            notifUser.MediaInfo.FixAlertText(
                                args.ChatMessage.DisplayName,
                                args.ChatMessage.Message
                            );

                            notifUser.MediaInfo.MetaInfo.Priority = MediaAlertPriority.High;

                            var mediaDto = new MediaDto { MediaInfo = notifUser.MediaInfo };

                            await _hubContext.Clients.All.Alert(mediaDto);
                        }

                        _users.Add(args.ChatMessage.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex);
                }
            },
            _token
        );
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="name"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public async Task<string?> TestVideo(string name, string? color = "white")
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(_token);
        var user = dbContext
            .HelloVideosUsers.AsNoTracking()
            .Include(e => e.MediaInfo)
            .AsEnumerable()
            .FirstOrDefault(e => name.Equals(e.Name, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            return null;
        }

        user.MediaInfo.FixAlertText(name, string.Empty);
        user.MediaInfo.TextInfo.KeyWordsColor = color;

        var mediaDto = new MediaDto() { MediaInfo = user.MediaInfo };

        await _hubContext.Clients.All.Alert(mediaDto);
        return user.Name;
    }
}
