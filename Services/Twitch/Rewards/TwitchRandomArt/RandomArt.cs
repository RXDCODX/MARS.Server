using BooruSharp.Booru;
using BooruSharp.Search.Post;
using MARS.Server.Services.ServiceManager;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards.TwitchRandomArt;

public class RandomArt(
    IHubContext<TelegramusHub, ITelegramusHub> hub,
    IHostApplicationLifetime lifetime,
    ITwitchClient client,
    Gelbooru site,
    ILogger<RandomArt> logger,
    EventSubWebsocketClient wsClient,
    SharedOptions staticFilesOptions
) : ManagedServiceBase(logger)
{
    public override string ServiceName => "randomart";
    public override string DisplayName => "Random Art";
    public override string Description => "Случайное искусство на Twitch";
    public override bool IsServiceActive { get; set; }

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
            && IsServiceActive
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                if (twEvent.UserInput.Contains("rating", StringComparison.OrdinalIgnoreCase))
                {
                    await client.SendMessageToMainTwitchAsync(
                        @$"@{twEvent.UserName}, ты охуел?",
                        logger
                    );
                    return;
                }

                var result = new List<SearchResult>();

                if (int.TryParse(twEvent.UserInput, out var aa))
                {
                    do
                    {
                        var answer = await site.GetRandomPostsAsync(10, "rating:general");

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
                        var answer = await site.GetRandomPostsAsync(
                            10,
                            string.Join(' ', tagParams)
                        );

                        if (answer.Length == 0)
                        {
                            await client.SendMessageToMainTwitchAsync(
                                @$"@{twEvent.UserName}, плохой запрос, нету артов(",
                                logger
                            );
                            return;
                        }

                        var posts = answer.Where(e => e.Rating is Rating.General or Rating.Safe);

                        result.AddRange(posts);
                    } while (result.Count == 0);
                }

                var mediaDtos = new MediaDto[result.Count];
                var index = 0;
                result = [.. result.DistinctBy(e => e.ID)];

                foreach (var sr in result)
                {
                    var fileUrl = sr.FileUrl.AbsoluteUri;
                    var extension = Path.GetExtension(fileUrl);
                    var fileName = Path.GetFileName(fileUrl);
                    var mediaType = await extension.GetFileMediaTypeAsync();
                    var staticFilePath =
                        staticFilesOptions.RequestPath.HasValue
                        && staticFilesOptions.RequestPath.Value.EndsWith('/')
                            ? staticFilesOptions.RequestPath.Value
                            : staticFilesOptions.RequestPath.Value + '/';

                    var mediaDto = new MediaDto()
                    {
                        MediaInfo = new MediaInfo
                        {
                            FileInfo = new MediaFileInfo
                            {
                                Extension = extension,
                                FileName = fileName,
                                FilePath = staticFilePath + fileUrl,
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

                await hub.Clients.All.Alerts(mediaDtos);
            });
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        return base.StopAsync(cancellationToken);
    }
}
