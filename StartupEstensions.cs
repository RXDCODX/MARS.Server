using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Platforms.YouTube;
using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services.TelegramBotService.Commands;
using MARS.Server.Services.Twitch.AutoInfoFetch;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages;
using MARS.Server.Services.Twitch.ClientMessages.SignalRAlerts;
using MARS.Server.Services.Twitch.ClientMessages.TekkenFrameData;
using MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;
using MARS.Server.Services.Twitch.FumoFriday;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.MiniGamesStats;
using MARS.Server.Services.Twitch.Rewards.CloseGameReward;
using MARS.Server.Services.Twitch.Rewards.MiniGames;
using MARS.Server.Services.Twitch.Rewards.TwitchAlerts;
using MARS.Server.Services.Twitch.Rewards.TwitchClipCreator;
using MARS.Server.Services.Twitch.Rewards.TwitchHighlitedMessage;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomArt;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;
using MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.SoundBarService;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using Microsoft.OpenApi.Models;
using NJsonSchema.Generation;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Websockets.Extensions;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace MARS.Server;

public static class StartupEstensions
{
    internal static IServiceCollection AddBaseAspNetMiddlewares(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services
            .AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            });
        services.AddDirectoryBrowser();
        services.AddCors(options =>
            options.AddPolicy(
                "CorsPolicy",
                builder =>
                {
                    builder
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(host => true)
                        .AllowCredentials();
                }
            )
        );

        return services;
    }

    internal static IServiceCollection AddTelegramThings(
        this IServiceCollection services,
        ILoggerFactory factory
    )
    {
        BotConfiguration botConfig;

        services
            .AddHttpClient("telegram_bot_client")
            .AddTypedClient<ITelegramBotClient>(
                (httpClient, sp) =>
                {
                    botConfig = sp.GetConfiguration<BotConfiguration>();
                    TelegramBotClientOptions options = new(botConfig.BotToken);

                    return new TelegramBotClient(options, httpClient);
                }
            );

        services.AddSingleton<WTelegramClient>(
            (sp) =>
            {
                var options = sp.GetRequiredService<IOptions<WTelegramClientConfiguration>>().Value;
                var client = new WTelegramClient(
                    options.AppId,
                    options.ApiHash,
                    "bin/WTelegram.session"
                );
                var logger = factory.CreateLogger("WTelegram");
                WTelegram.Helpers.Log = (i, v) => logger.Log((LogLevel)i, "{Message}", [v]);
                client.LoginUserIfNeeded();
                //DoLogin(client, options.PhoneNumber, options).GetAwaiter().GetResult();

                return client;

                //static async Task DoLogin(
                //    Client client,
                //    string loginInfo,
                //    WTelegramClientConfiguration configuration
                //) // (add this method to your code)
                //{
                //    while (client.User == null)
                //    {
                //        switch (await client.Login(loginInfo)) // returns which config is needed to continue login
                //        {
                //            case "verification_code":
                //                Console.Write("Code: ");
                //                loginInfo =
                //                    Console.ReadLine() ?? throw new NullReferenceException();
                //                break;
                //            case "name":
                //                loginInfo = configuration.FirstNameLastName;
                //                break; // if sign-up is required (first/last_name)
                //            case "password":
                //                loginInfo = configuration.Password;
                //                break; // if user has enabled 2FA
                //            default:
                //                loginInfo = string.Empty;
                //                break;
                //        }
                //    }
                //}
            }
        );

        services.AddScoped<Commands>();
        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();

        return services;
    }

    internal static async Task<IServiceCollection> AddTwitchEvents(
        this IServiceCollection services,
        IConfigurationManager manager,
        ILoggerFactory factory
    )
    {
        var twitchConfig = new TwitchConfiguration();
        var twitchConfigSection = manager
            .GetSection(AppBase.Base)
            .GetSection(TwitchConfiguration.SectionName);

        services.Configure<TwitchConfiguration>(twitchConfigSection);
        twitchConfigSection.Bind(twitchConfig);
        var twitchApi = new TwitchAPI { Settings = { ClientId = twitchConfig.ClientId } };
        twitchApi.Settings.AccessToken = await twitchApi.Auth.GetAccessTokenAsync();
        twitchApi.Settings.Secret = twitchConfig.ClientSecret;
        twitchApi.Settings.Scopes = [AuthScopes.Any];

        services.AddSingleton<ITwitchAPI>(twitchApi);

        var credentials = new ConnectionCredentials(TwitchExstension.BotName, twitchConfig.OAuth);

        var client = new TwitchClient(default, default, factory.CreateLogger<TwitchClient>());

        client.Initialize(credentials, TwitchExstension.Channel);
        client.Connect();
        services.AddSingleton<ITwitchClient>(client);

        services.AddTwitchLibEventSubWebsockets();

        services.AddSingleton<TwitchStreamStartupNotifications>();
        services.AddSingleton<TwitchMediaAlerts>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMediaAlerts>());
        services.AddSingleton<AutoMessagesController>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoMessagesController>());
        services.AddSingleton<TwitchAuthService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchAuthService>());
        services.AddSingleton<EventSubService>();
        services.AddSingleton<TelegramTokenNotification>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<AutoHello>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoHello>());

        services.AddSingleton<AddNewWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<AddNewWaifu>());
        services.AddSingleton<MergeWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<MergeWaifu>());
        services.AddSingleton<RollWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<RollWaifu>());
        services.AddSingleton<RandomMeme>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomMeme>());
        services.AddSingleton<TwitchRussianRoulete>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchRussianRoulete>());
        services.AddSingleton<TekkenVictorina>();
        services.AddHostedService(sp => sp.GetRequiredService<TekkenVictorina>());
        services.AddSingleton<TwitchTrivia>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchTrivia>());
        services.AddSingleton<HighlitedMessage>();
        services.AddHostedService(sp => sp.GetRequiredService<HighlitedMessage>());
        services.AddSingleton<FumoFridayWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<FumoFridayWorker>());
        services.AddSingleton<HelloVideoWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<HelloVideoWorker>());

        services.AddSingleton<Confetty>();
        services.AddSingleton<Fireworks>();
        services.AddSingleton<Emojis>();
        services.AddHostedService(sp => sp.GetRequiredService<Confetty>());
        services.AddHostedService(sp => sp.GetRequiredService<Fireworks>());
        services.AddHostedService(sp => sp.GetRequiredService<Emojis>());

        services.AddSingleton<RandomArt>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomArt>());

        services.AddSingleton<TwitchMessagesHubAwaker>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMessagesHubAwaker>());

        services.AddSingleton<SoundBarFactory>();

        services.AddSingleton<AutoRewardInfoFetcher>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoRewardInfoFetcher>());

        services.AddSingleton<Tekken8FrameData>();
        services.AddHostedService(sp => sp.GetRequiredService<Tekken8FrameData>());

        services.AddSingleton<TwitchFramedate>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchFramedate>());

        services.AddSingleton<MiniGamesManager>();
        services.AddHostedService(sp => sp.GetRequiredService<MiniGamesManager>());

        services.AddSingleton<TekkenVictorinaLeaderbord>();
        services.AddHostedService(sp => sp.GetRequiredService<TekkenVictorinaLeaderbord>());

        services.AddSingleton<TwitchClipCreatorService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchClipCreatorService>());

        services.AddSingleton<TwitchCloseTekkenService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchCloseTekkenService>());

        services.AddSingleton<TwitchNameActualizer>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchNameActualizer>());

        services.AddSingleton<ServiceManager>();
        services.AddSingleton<IServiceManager>(sp => sp.GetRequiredService<ServiceManager>());

        return services;
    }

    internal static IServiceCollection AddConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<BotConfiguration>(
            configuration
                .GetSection(AppBase.Base)
                .GetSection(TelegramConfiguration.TelegramSection)
                .GetSection(BotConfiguration.Configuration)
        );

        services.Configure<TelegramConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(TelegramConfiguration.TelegramSection)
        );
        services.Configure<HttpClientsConfiguration>(
            configuration
                .GetSection(AppBase.Base)
                .GetSection(HttpClientsConfiguration.Configuration)
        );
        services.Configure<ShikimoriClientOptions>(
            configuration.GetSection(AppBase.Base).GetSection(ShikimoriClientOptions.Options)
        );
        services.Configure<DiscordConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(DiscordConfiguration.Configuration)
        );
        services.Configure<YouTubeConfig>(
            configuration.GetSection(AppBase.Base).GetSection(YouTubeConfig.SectionName)
        );
        services.Configure<ChannelsSpy>(configuration.GetSection(ChannelsSpy.Configuration));
        services.Configure<Config365>(
            configuration.GetSection(AppBase.Base).GetSection(Config365.Configuration)
        );
        services.Configure<VkConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(VkConfiguration.SectionName)
        );
        services.Configure<BooruConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(BooruConfiguration.Section)
        );
        services.Configure<WTelegramClientConfiguration>(
            configuration
                .GetSection(AppBase.Base)
                .GetSection(WTelegramClientConfiguration.TelegramSection)
        );
        services.Configure<HoyolabConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(HoyolabConfiguration.Section)
        );
        services.Configure<YandexMusicConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(YandexMusicConfiguration.SectionName)
        );

        return services;
    }

    public static IServiceCollection AddSoundRequest(this IServiceCollection services)
    {
        Program.IsUseSoundRequest = true;

        services.AddSingleton<YouTubeApiService>();

        services.AddSingleton<SoundRequestBackendPlayer>();
        services.AddHostedService(sp => sp.GetRequiredService<SoundRequestBackendPlayer>());

        services.AddSingleton<SoundRequestBackgroundPlaylist>();
        services.AddHostedService(sp => sp.GetRequiredService<SoundRequestBackgroundPlaylist>());
        services.AddSingleton<SoundRequestHistoryService>();
        services.AddSingleton<SoundRequestHandler>();
        services.AddSingleton<SoundRequestUserQueue>();
        services.AddSingleton<SoundRequestSignalREvents>();

        return services;
    }

    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        Program.IsUseSwagger = true;
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo() { Version = "v1", Title = "Telegramus Open Api v3" }
            );
            options.AddSignalRSwaggerGen();
            options.UseAllOfToExtendReferenceSchemas();
            //options.UseAllOfForInheritance();
            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();
            options.UseInlineDefinitionsForEnums();
        });
        services.AddSwaggerDocument(configure =>
        {
            configure.Title = "Telegramus";
            configure.DefaultResponseReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull;
            configure.AllowNullableBodyParameters = false;
        });

        return services;
    }

    internal static IServiceCollection AddYandexMusic(this IServiceCollection services)
    {
        services.AddSingleton<IYandexMusicMainResolver>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("YandexMusicResolver");
            var config = sp.GetRequiredService<IOptions<YandexMusicConfiguration>>();
            var authService = YandexMusicAuthService.Create(httpClient);
            var result = authService
                .LoginAsync(config.Value.Login, config.Value.Password)
                .GetAwaiter()
                .GetResult();
            var credentialProvider = YandexCredentialsProvider.Create(
                authService,
                config.Value.Login,
                config.Value.Password
            );
            var yandexMusicMainResolver = YandexMusicMainResolver.Create(
                credentialProvider,
                httpClient
            );

            return yandexMusicMainResolver;
        });

        return services;
    }
}
