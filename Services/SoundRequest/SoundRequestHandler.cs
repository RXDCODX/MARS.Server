using MARS.Server.Services.SoundRequest.Entitys;
using MARS.Server.Services.Twitch.Management;

namespace MARS.Server.Services.SoundRequest;

public class SoundRequestHandler(
    IHostApplicationLifetime lifetime,
    //IDbContextFactory<AppDbContext> factory,
    ITwitchClient client,
    IHttpClientFactory httpClientFactory
) : BackgroundService
{
    private readonly CancellationToken _cancellationToken = lifetime.ApplicationStopping;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            EventSubService.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
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
        var url = twEvent.UserInput;

        if (channel.Equals(TwitchExstension.Channel))
        {
            await Task.Factory.StartNew(
                async () =>
                {
                    if (
                        string.IsNullOrWhiteSpace(url)
                        || !Uri.TryCreate(url, UriKind.Absolute, out var result)
                    )
                    {
                        await client.SendMessageToMainTwitchAsync("Кривая ссылка");
                        return;
                    }

                    SoundRequestDomainSource soundRequestDomainSource = GetDomainType(result);
                    if (soundRequestDomainSource == SoundRequestDomainSource.None)
                    {
                        await client.SendMessageToMainTwitchAsync("Кривая ссылка");
                        return;
                    }

                    using var httpClient = httpClientFactory.CreateClient();
                    var baseTrackInfo = soundRequestDomainSource switch
                    {
                        SoundRequestDomainSource.Youtube => await GetYoutubeBaseTrackInfo(
                            httpClient
                        ),
                        //SoundRequestDomainSource.SoundCloud => await GetSoundCloudBaseTrackInfo(
                        //    httpClient
                        //),
                        //SoundRequestDomainSource.YandexMusic => await GetYandexMusicBaseTrackInfo(
                        //    httpClient
                        //),
                        //SoundRequestDomainSource.VkMusic => await GetVkMusicBaseTrackInfo(
                        //    httpClient
                        //),
                        _ => throw new InvalidOperationException(),
                    };
                },
                _cancellationToken
            );
        }
    }

    private Task<BaseTrackInfo> GetVkMusicBaseTrackInfo(HttpClient httpClient)
    {
        throw new NotImplementedException();
    }

    private Task<BaseTrackInfo> GetYandexMusicBaseTrackInfo(HttpClient httpClient)
    {
        throw new NotImplementedException();
    }

    private Task<BaseTrackInfo> GetSoundCloudBaseTrackInfo(HttpClient httpClient)
    {
        throw new NotImplementedException();
    }

    private Task<BaseTrackInfo> GetYoutubeBaseTrackInfo(HttpClient httpClient)
    {
        throw new NotImplementedException();
    }

    private static SoundRequestDomainSource GetDomainType(Uri uri)
    {
        return uri.Host switch
        {
            "youtu.be" or "youtube.com" => SoundRequestDomainSource.Youtube,
            "soundcloud.com" => SoundRequestDomainSource.SoundCloud,
            "music.yandex.ru" => SoundRequestDomainSource.YandexMusic,
            "vk.com" => SoundRequestDomainSource.VkMusic,
            _ => SoundRequestDomainSource.None,
        };
    }
}
