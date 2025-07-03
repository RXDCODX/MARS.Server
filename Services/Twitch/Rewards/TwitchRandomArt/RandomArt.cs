using BooruSharp.Booru;
using BooruSharp.Search.Post;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomArt;

public class RandomArt : BackgroundService
{
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hub;
    private readonly ITwitchClient _client;
    private readonly Gelbooru _site;
    private readonly ILogger<RandomArt> _logger;

    public RandomArt(
        IHubContext<TelegramusHub, ITelegramusHub> hub,
        IHostApplicationLifetime lifetime,
        ITwitchClient client,
        Gelbooru site,
        ILogger<RandomArt> logger
    )
    {
        _hub = hub;
        _client = client;
        _site = site;
        _logger = logger;
        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.Reward.Cost == 27
            && twEvent.BroadcasterUserLogin.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                if (twEvent.UserInput.Contains("rating", StringComparison.OrdinalIgnoreCase))
                {
                    await _client.SendMessageToMainTwitchAsync(
                        @$"@{twEvent.UserName}, ты охуел?",
                        _logger
                    );
                    return;
                }

                var result = new List<SearchResult>();

                if (int.TryParse(twEvent.UserInput, out var aa))
                {
                    do
                    {
                        var answer = await _site.GetRandomPostsAsync(10, "rating:general");

                        var posts = answer.Where(e =>
                            e.Rating is Rating.General or Rating.Questionable
                        );

                        result.AddRange(posts);
                    } while (result.Count == 0);
                }
                else
                {
                    do
                    {
                        var tagParams = (twEvent.UserInput + " rating:general").Split(' ');
                        var answer = await _site.GetRandomPostsAsync(
                            10,
                            string.Join(' ', tagParams)
                        );

                        if (answer.Length == 0)
                        {
                            await _client.SendMessageToMainTwitchAsync(
                                @$"@{twEvent.UserName}, плохой запрос, нету артов(",
                                _logger
                            );
                            return;
                        }

                        var posts = answer.Where(e => e.Rating is Rating.General or Rating.Safe);

                        result.AddRange(posts);
                    } while (result.Count == 0);
                }

                var mediaDtos = new MediaDto[result.Count];
                var index = 0;
                result = result.DistinctBy(e => e.ID).ToList();

                foreach (var sr in result)
                {
                    var fileUrl = sr.FileUrl.AbsoluteUri;
                    var extension = Path.GetExtension(fileUrl);
                    var fileName = Path.GetFileName(fileUrl);
                    var mediaType = await extension.GetFileMediaTypeAsync();

                    var mediaDto = new MediaDto()
                    {
                        MediaInfo = new MediaInfo
                        {
                            FileInfo = new MediaFileInfo
                            {
                                Extension = extension,
                                FileName = fileName,
                                FilePath = fileUrl,
                                Type = mediaType,
                                IsLocalFile = false,
                            },
                            MetaInfo = new MediaMetaInfo { DisplayName = twEvent.UserName },
                            PositionInfo = new MediaPositionInfo(),
                            StylesInfo = new MediaStylesInfo(),
                            TextInfo = new MediaTextInfo(),
                        },
                    };

                    mediaDtos[index++] = mediaDto;
                }

                await _hub.Clients.All.Alerts(mediaDtos);
            });
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
