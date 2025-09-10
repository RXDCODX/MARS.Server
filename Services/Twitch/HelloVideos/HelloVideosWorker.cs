using MARS.Server.Services.ServiceManager;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.HelloVideos;

public class HelloVideoWorker(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<HelloVideoWorker> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ITwitchClient client
) : ManagedServiceBase(logger)
{
    private readonly CancellationToken _token = hostApplicationLifetime.ApplicationStopping;
    private readonly List<string> _users = [];
    public override string ServiceName => "hellovideo";
    public override string DisplayName => "Hello Videos";
    public override string Description => "Hello Videos Twitch интеграция";
    public override bool IsServiceActive { get; set; }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += OnMessageReceived;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        client.OnMessageReceived -= OnMessageReceived;

        return base.StopAsync(cancellationToken);
    }

    public async void OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if ((args.ChatMessage.Channel != TwitchExstension.Channel || !IsServiceActive))
        {
            return;
        }

        if (
            !TwitchExstension.BlackList.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        var now = DateTimeOffset.Now;
                        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                            _token
                        );
                        var user = await dbContext.FumoUsers.FindAsync(
                            args.ChatMessage.UserId,
                            _token
                        );

                        if (now.DayOfWeek == DayOfWeek.Friday && user != null)
                        {
                            return;
                        }

                        if (_users.Contains(args.ChatMessage.Id))
                        {
                            return;
                        }

                        var notifUser = await dbContext
                            .HelloVideosUsers.Include(e => e.MediaInfo)
                            .FirstOrDefaultAsync(
                                e => e.TwitchId == args.ChatMessage.UserId,
                                _token
                            );

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

                                await hubContext.Clients.All.Alert(mediaDto);
                            }

                            _users.Add(args.ChatMessage.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
                    }
                },
                _token
            );
        }
    }

    public async Task<string?> TestVideo(string name, string? color = "white")
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(_token);
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

        await hubContext.Clients.All.Alert(mediaDto);
        return user.Name;
    }
}
