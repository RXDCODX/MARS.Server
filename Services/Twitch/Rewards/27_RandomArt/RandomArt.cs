using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.Twitch.Rewards._27_RandomArt;

public class RandomArt(
    IHubContext<TelegramusHub, ITelegramusHub> hub,
    ITwitchClient client,
    DanbooruRandomPostService site,
    ILogger<RandomArt> logger,
    EventSubWebsocketClient wsClient,
    SharedOptions staticFilesOptions,
    RickRollerService rickRollerService,
    RandomArt_TwitchReward reward,
    ITwitchEventValidationService validator
) : BackgroundService, ITwitchReward
{
    public int Cost { get; init; } = reward.Cost;

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object? sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var vr = await validator
            .ForRedemption(args)
            .RequireBroadcasterUserId()
            .RequireCost(Cost)
            .RequireFollower()
            .ValidateAsync();

        if (vr.IsInvalid)
        {
            await client.SendMessageToMainTwitchAsync(
                $"@{args.Payload.Event.UserName}, " + vr.FirstError
            );
            return;
        }

        var twEvent = args.Payload.Event;

        await Task.Factory.StartNew(async () =>
        {
            await rickRollerService.TryRickRollAsync(
                TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args)!,
                async () =>
                {
                    var userInput = twEvent.UserInput.Trim();

                    if (string.IsNullOrWhiteSpace(userInput))
                    {
                        await client.SendMessageToMainTwitchAsync(
                            @$"@{twEvent.UserName}, нужен хотя бы один тег.",
                            logger
                        );
                        return;
                    }

                    if (userInput.Contains(' '))
                    {
                        await client.SendMessageToMainTwitchAsync(
                            @$"@{twEvent.UserName}, нужен только один тег без пробелов.",
                            logger
                        );
                        return;
                    }

                    var searchQuery = $"{userInput} rating:general";
                    var searchResult = await site.GetRandomPostAsync(searchQuery);
                    var answer = searchResult;

                    if (answer is not { Length: > 0 })
                    {
                        await client.SendMessageToMainTwitchAsync(
                            @$"@{twEvent.UserName}, плохой запрос, нету артов(",
                            logger
                        );
                        return;
                    }

                    var result = answer.DistinctBy(e => e.Id).ToList();
                    var mediaDtos = new MediaDto[result.Count];
                    var index = 0;

                    foreach (var preview in result)
                    {
                        var post = preview;
                        var fileUrl = post.LargeFileUrl ?? post.FileUrl ?? post.PreviewFileUrl;

                        if (string.IsNullOrWhiteSpace(fileUrl))
                        {
                            await client.SendMessageToMainTwitchAsync(
                                @$"@{twEvent.UserName}, не удалось получить ссылку на арт.",
                                logger
                            );
                            return;
                        }

                        var extension = Path.GetExtension(fileUrl);
                        var fileName = Path.GetFileName(fileUrl);
                        var mediaType = await extension.GetFileMediaTypeAsync();
                        var staticFilePath =
                            staticFilesOptions.RequestPath.HasValue
                            && staticFilesOptions.RequestPath.Value.EndsWith('/')
                                ? staticFilesOptions.RequestPath.Value
                                : staticFilesOptions.RequestPath.Value + '/';

                        var mediaDto = new MediaDto
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

                    await hub.Clients.All.Alerts(mediaDtos);
                }
            );
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd +=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        wsClient.ChannelPointsCustomRewardRedemptionAdd -=
            WsClientOnChannelPointsCustomRewardRedemptionAdd;
        await base.StopAsync(cancellationToken);
    }
}
