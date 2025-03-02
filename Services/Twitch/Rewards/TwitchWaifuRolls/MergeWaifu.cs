using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using TwitchLib.Api.Helix.Models.Chat;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class MergeWaifu : BackgroundService
{
    private readonly ITwitchAPI _api;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<MergeWaifu> _logger;
    private readonly WaifuRollService _waifuRollService;
    private readonly TokenService _tokenService;

    public MergeWaifu(
        ILogger<MergeWaifu> logger,
        ITwitchClient client,
        WaifuRollService waifuRollService,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IDbContextFactory<AppDbContext> factory,
        WaifuRollDataBaseHelper dataBaseHelper,
        ITwitchAPI api,
        EventSubService eventSubService,
        TokenService tokenService
    )
    {
        _logger = logger;
        _client = client;
        _waifuRollService = waifuRollService;
        _hubContext = hubContext;
        _factory = factory;
        _api = api;
        _tokenService = tokenService;

        eventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd += MergeWaifuTwitchEvent;
    }

    public async Task MergeWaifuTwitchEvent(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (twEvent.Reward.Cost == 2)
            {
                await using AppDbContext dbContext = await _factory.CreateDbContextAsync();
                var isExist = dbContext.Hosts.Find(twEvent.UserId) != null;
                if (isExist)
                {
                    var host = await dbContext.Hosts.FindAsync(twEvent.Id);

                    if (host is not null && host.TwitchId != twEvent.UserId && !host.IsPrivated)
                    {
                        var waifu = dbContext.Waifus.Find(host.WaifuRollId);
                        if (waifu != null)
                        {
                            var isMerged = await _waifuRollService.MergeTheWaifu(host, waifu);
                            if (isMerged)
                            {
                                var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                    twEvent.UserName,
                                    AnswersForTwitchRewards.Answers[Command.MergeWaifu],
                                    null,
                                    null,
                                    waifu
                                );

                                waifu.IsMerged = true;

                                await _hubContext.Clients.All.MergeWaifu(waifu, twEvent.UserName);

                                if (_tokenService.Token != null)
                                {
                                    await _api.Helix.Chat.SendChatAnnouncementAsync(
                                        TwitchExstension.ChannelId,
                                        TwitchExstension.ChannelId,
                                        message,
                                        AnnouncementColors.Primary,
                                        _tokenService.Token.AccessToken
                                    );
                                }

                                return;
                            }
                        }
                        else
                        {
                            var tempLate3 = "@{user}, не удалось найти твою невестку в бд :-(";
                            var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                twEvent.UserName,
                                tempLate3
                            );
                            await _client.SendMessageToMainTwitchAsync(message, _logger);

                            return;
                        }
                    }
                    else if (host != null && host.TwitchId != twEvent.UserId)
                    {
                        host.TwitchId = twEvent.UserId;
                        host.Name = twEvent.UserName;

                        dbContext.Hosts.Add(host);

                        await dbContext.SaveChangesAsync();

                        var tempLate2 = "@{user}, ты новенький, тебе пока нельзя!";
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate2
                        );
                        await _client.SendMessageToMainTwitchAsync(message, _logger);
                        return;
                    }
                    else
                    {
                        var tempLate2 = "@{user}, ты уже женат, сорян!";
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            twEvent.UserName,
                            tempLate2
                        );
                        await _client.SendMessageToMainTwitchAsync(message, _logger);
                        return;
                    }
                }

                var tempLate = "@{user}, не удалось устроить твою свадьбу";
                var resultMessage = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                    twEvent.UserName,
                    tempLate
                );
                await _client.SendMessageToMainTwitchAsync(resultMessage, _logger);
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
