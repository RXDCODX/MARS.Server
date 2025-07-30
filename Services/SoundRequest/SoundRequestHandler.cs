using MARS.Server.Services.SoundRequest.Entitys;
using MARS.Server.Services.SoundRequest.Entitys.Exceptions;
using MARS.Server.Services.SoundRequest.Platforms.SoundCloud;
using MARS.Server.Services.SoundRequest.Platforms.YouTube;
using TwitchLib.EventSub.Websockets;

namespace MARS.Server.Services.SoundRequest;

/// <summary>
/// Handles the logic for processing and managing sound requests.
/// </summary>
public class SoundRequestHandler(
    IHostApplicationLifetime lifetime,
    ITwitchClient client,
    ILogger<SoundRequestHandler> logger,
    IOptions<YouTubeConfig> youtubeConfig,
    YouTubeApiService youTubeApiService,
    SoundCloudApiService soundCloudApiService,
    SoundCloudTextSearchService soundCloudTextSearchService,
    SoundRequestUserQueue userQueue,
    EventSubWebsocketClient wsClient
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            wsClient.ChannelPointsCustomRewardRedemptionAdd +=
                WsClientOnChannelPointsCustomRewardRedemptionAdd;
        });

        return Task.CompletedTask;
    }

    private async Task WsClientOnChannelPointsCustomRewardRedemptionAdd(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        var channel = twEvent.BroadcasterUserLogin;
        var userInput = twEvent.UserInput;

        if (!channel.Equals(TwitchExstension.Channel))
        {
            return;
        }

        await Task.Factory.StartNew(
            async () =>
            {
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    await client.SendMessageToMainTwitchAsync("Пустой запрос");
                    return;
                }

                if (Uri.TryCreate(userInput, UriKind.Absolute, out var uri))
                {
                    await HandleUrlRequest(uri, userInput, twEvent.UserId, twEvent.UserName);
                }
                else
                {
                    await HandleTextRequest(userInput, twEvent.UserId, twEvent.UserName);
                }
            },
            _cancellationToken
        );
    }

    private async Task HandleUrlRequest(Uri uri, string url, string userId, string userName)
    {
        var domainType = GetDomainType(uri);
        if (domainType == SoundRequestDomainSource.None)
        {
            await client.SendMessageToMainTwitchAsync("Кривая или неподдерживаемая ссылка");
            return;
        }

        try
        {
            var baseTrackInfo = domainType switch
            {
                SoundRequestDomainSource.Youtube =>
                    await youTubeApiService.GetYoutubeBaseTrackInfoAsync(
                        url,
                        youtubeConfig.Value.Token,
                        _cancellationToken
                    ),
                SoundRequestDomainSource.SoundCloud =>
                    await soundCloudApiService.GetSoundCloudBaseTrackInfoAsync(
                        url,
                        _cancellationToken
                    ),
                //SoundRequestDomainSource.YandexMusic =>
                //    await yandexMusicApiService.GetYandexMusicBaseTrackInfoAsync(
                //        url,
                //        _cancellationToken
                //    ),
                // SoundRequestDomainSource.VkMusic => await GetVkMusicBaseTrackInfo(...),
                _ => throw new InvalidOperationException(),
            };

            await userQueue.AddToQueueAsync(
                new UserRequestedTrack
                {
                    RequestedTrack = baseTrackInfo,
                    TwitchId = userId,
                    TwitchDisplayName = userName,
                    RequestedTrackId = baseTrackInfo.Id,
                }
            );
        }
        catch (Exception ex)
        {
            await client.SendMessageToMainTwitchAsync($"Ошибка при обработке ссылки: {ex.Message}");
            logger.LogException(ex);
        }
    }

    private async Task HandleTextRequest(string query, string userId, string userName)
    {
        try
        {
            BaseTrackInfo? baseTrackInfo = null;
            // Если не найдено в Яндекс.Музыке, ищем в SoundCloud
            try
            {
                baseTrackInfo = await soundCloudTextSearchService.SearchTrackAsync(
                    query,
                    _cancellationToken
                );
            }
            catch (TrackNotFoundException scEx)
            {
                await client.SendMessageToMainTwitchAsync(
                    $"Трек не найден ни в Яндекс.Музыке, ни в SoundCloud: {scEx.Message}"
                );
                logger.LogException(scEx);
                return;
            }

            await userQueue.AddToQueueAsync(
                new UserRequestedTrack
                {
                    RequestedTrack = baseTrackInfo,
                    TwitchId = userId,
                    TwitchDisplayName = userName,
                    RequestedTrackId = baseTrackInfo.Id,
                }
            );
        }
        catch (Exception ex)
        {
            await client.SendMessageToMainTwitchAsync($"Ошибка поиска по тексту: {ex.Message}");
            logger.LogException(ex);
        }
    }

    private static SoundRequestDomainSource GetDomainType(Uri uri)
    {
        return uri.Host switch
        {
            "youtu.be" or "youtube.com" => SoundRequestDomainSource.Youtube,
            "soundcloud.com" => SoundRequestDomainSource.SoundCloud,
            "music.yandex.ru" => SoundRequestDomainSource.YandexMusic,
            //"vk.com" => SoundRequestDomainSource.VkMusic,
            _ => SoundRequestDomainSource.None,
        };
    }
}
