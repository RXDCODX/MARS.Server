using System.Text.Json;
using System.Text.Json.Serialization;
using BooruSharp.Booru;
using MARS.Server.CustomLoggers.SignalRLogger;
using MARS.Server.Services._365Genius;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Honkai;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using MARS.Server.Services.Scoreboard;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.SoundRequest_OBSOLETE;
using MARS.Server.Services.SoundRequest_OBSOLETE.Platforms.YouTube;
using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services.Twitch.AutoInfoFetch;
using MARS.Server.Services.Twitch.Client;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Extensions;
using MARS.Server.Services.Twitch.ClientMessages.SignalRAlerts;
using MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;
using MARS.Server.Services.Twitch.FumoFriday;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.MiniGamesStats;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.Rewards.CloseGameReward;
using MARS.Server.Services.Twitch.Rewards.MiniGames;
using MARS.Server.Services.Twitch.Rewards.TestReward;
using MARS.Server.Services.Twitch.Rewards.TwitchAdhdReward;
using MARS.Server.Services.Twitch.Rewards.TwitchAlerts;
using MARS.Server.Services.Twitch.Rewards.TwitchClipCreator;
using MARS.Server.Services.Twitch.Rewards.TwitchCredits;
using MARS.Server.Services.Twitch.Rewards.TwitchGaoAlert;
using MARS.Server.Services.Twitch.Rewards.TwitchMichaelJacksonReward;
using MARS.Server.Services.Twitch.Rewards.TwitchHighlitedMessage;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomArt;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;
using MARS.Server.Services.Twitch.Rewards.TwitchRefundService;
using MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.SoundBarService;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using MARS.Server.Services.Twitch.StreamManagement;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.Twitch.TwitchFollowers;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.Entitys.Interfaces;
using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Swagger;
using Microsoft.OpenApi.Models;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.HttpCallHandlers;
using TwitchLib.Api.Core.Interfaces;
using TwitchLib.EventSub.Websockets.Extensions;
using YandexMusicResolver;
using YandexMusicResolver.Config;

namespace MARS.Server;

public static class StartupEstensions
{
    internal static IServiceCollection AddBaseAspNetMiddlewares(this IServiceCollection services)
    {
        services.AddSpaYarp();

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
                if (
                    !File.Exists(
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "WTelegram",
                            "WTelegram.session"
                        )
                    )
                )
                {
                    throw new FileNotFoundException("WTelegram session not found");
                }
                var client = new WTelegramClient(
                    options.AppId,
                    options.ApiHash,
                    "WTelegram/WTelegram.session"
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

        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();

        // Регистрируем ScoreboardService
        services.AddScoped<ScoreboardService>();

        return services;
    }

    internal static IServiceCollection AddTwitchEvents(
        this IServiceCollection services,
        IConfigurationManager manager
    )
    {
        var twitchConfig = new TwitchConfiguration();
        var twitchConfigSection = manager
            .GetSection(AppBase.Base)
            .GetSection(TwitchConfiguration.SectionName);

        services.Configure<TwitchConfiguration>(twitchConfigSection);
        twitchConfigSection.Bind(twitchConfig);

        services.AddSingleton<IRateLimiter, TwitchApiRateLimiter>();

        // Регистрируем обертку с рейт лимитером как основную реализацию ITwitchAPI
        services.AddSingleton<ITwitchAPI>(sp =>
        {
            var twitchApi = new TwitchAPI(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IRateLimiter>(),
                new ApiSettings() { ClientId = twitchConfig.ClientId },
                new TwitchHttpClient(sp.GetRequiredService<ILogger<TwitchHttpClient>>())
            )
            {
                Settings =
                {
                    //twitchApi.Settings.AccessToken = twitchApi.Auth.GetAccessTokenAsync().GetAwaiter().GetResult();
                    Secret = twitchConfig.ClientSecret,
                    Scopes = [AuthScopes.Any],
                },
            };

            return twitchApi;
        });

        services.AddSingleton<TwitchConnectionManager>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchConnectionManager>());
        services.AddSingleton<ITwitchClient>(sp =>
            sp.GetRequiredService<TwitchConnectionManager>().Client
        );

        services.AddTwitchLibEventSubWebsockets();
        services.AddHostedService<EventSubService>();

        services.AddSingleton<TwitchStreamStartupNotifications>();
        services.AddSingleton<TwitchMediaAlerts>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMediaAlerts>());
        services.AddSingleton<AutoMessagesHandler>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoMessagesHandler>());

        // Регистрируем сервис для работы с шаблонами сообщений Twitch
        // services.AddSingleton<TwitchMessageBuilderService>();
        // services.AddHostedService(sp => sp.GetRequiredService<TwitchMessageBuilderService>());

        services.AddSingleton<TelegramTokenNotification>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<EventSubService>();
        services.AddHostedService(sp => sp.GetRequiredService<EventSubService>());

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
        services.AddScoped<TwitchRussianRoulete>();
        services.AddScoped<TekkenVictorina>();
        services.AddScoped<TwitchTrivia>();
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

        services.AddSingleton<FramedataStagingService>();
        services.Configure<FramedataConfiguration>(
            manager.GetSection(AppBase.Base).GetSection(FramedataConfiguration.SectionName)
        );
        services.AddSingleton<Tekken8FrameData>();
        services.AddHostedService(sp => sp.GetRequiredService<Tekken8FrameData>());

        // Добавил в команды
        //services.AddSingleton<TwitchFramedate>();
        //services.AddHostedService(sp => sp.GetRequiredService<TwitchFramedate>());

        services.AddSingleton<MiniGamesManager>();
        services.AddHostedService(sp => sp.GetRequiredService<MiniGamesManager>());

        services.AddSingleton<TekkenVictorinaLeaderbord>();
        services.AddHostedService(sp => sp.GetRequiredService<TekkenVictorinaLeaderbord>());

        services.AddSingleton<TwitchClipCreatorService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchClipCreatorService>());

        services.AddSingleton<TwitchCloseTekkenService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchCloseTekkenService>());

        services.AddSingleton<TwitchAdhdService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchAdhdService>());

        services.AddSingleton<TwitchCreditsRewardService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchCreditsRewardService>());

        services.AddSingleton<TwitchMichaelJacksonRewardService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMichaelJacksonRewardService>());

        services.AddHostedService<TwitchRefundService>();

        services.AddSingleton<TestRewardService>();
        services.AddHostedService(sp => sp.GetRequiredService<TestRewardService>());

        services.AddSingleton<TwitchGaoAlert>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchGaoAlert>());

        services.AddSingleton<TwitchNameActualizer>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchNameActualizer>());

        services.AddSingleton<ChannelRewardsService>();
        //services.AddHostedService<AlertInitializationService>();

        services.AddSingleton<ServiceManager>();
        services.AddSingleton<IServiceManager>(sp => sp.GetRequiredService<ServiceManager>());

        // Регистрируем сервис для работы с зрителями канала rxdcodx
        services.AddRxdcodxViewersServiceAsSingleton();

        // Регистрируем сервис для работы с автоматическими сообщениями
        services.AddAutoMessagesService();

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
        services.Configure<KinopoiskConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(KinopoiskConfiguration.SectionName)
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
        services.Configure<KinopoiskConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(KinopoiskConfiguration.SectionName)
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
            options.AddSignalRSwaggerGen();
            options.UseAllOfToExtendReferenceSchemas();
            options.SupportNonNullableReferenceTypes();
            options.NonNullableReferenceTypesAsRequired();
            options.UseInlineDefinitionsForEnums();

            // Filters
            options.DocumentFilter<DotNetTypesDocumentFilter>();
            options.DocumentFilter<PathPartitionDocumentFilter>();

            // Two separate documents served by Swashbuckle
            options.SwaggerDoc("api", new OpenApiInfo { Title = "Telegramus API" });
            options.SwaggerDoc("hubs", new OpenApiInfo { Title = "Telegramus Hubs" });
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

    internal static WebApplication AddLogerHub(this WebApplication app)
    {
        app.MapHub<LoggerHub>("/hubs/logger");

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStarted.Register(() =>
        {
            var hub = app.Services.GetRequiredService<IHubContext<LoggerHub, ILoggerHub>>();
            SignalRLogger.HubContext = hub;
        });

        return app;
    }

    /// <summary>
    /// Групповая регистрация доменных сервисов MARS (Shikimori, WaifuRoll, RandomMem, Synthezia, 365, PyroAlerts, Gelbooru, Scoreboard)
    /// </summary>
    internal static IServiceCollection AddMarsDomainServices(this IServiceCollection services)
    {
        services
            .AddPyroAlertsServices()
            .AddShikimoriServices()
            .AddScoreboardServiceSingleton()
            .AddWaifuRollServices()
            .AddRandomMemServices()
            .AddSyntheziaServices()
            .Add365Services()
            .AddBooruServices();

        return services;
    }

    internal static IServiceCollection AddPyroAlertsServices(this IServiceCollection services)
    {
        services.AddSingleton<PyroAlertsHelper>();
        services.AddSingleton<PyroAlertsHandler>();
        return services;
    }

    internal static IServiceCollection AddShikimoriServices(this IServiceCollection services)
    {
        services.AddSingleton<IShikimoriRateLimiter, ShikimoriShikimoriRateLimiter>();
        services.AddSingleton<ShikimoriService>();
        return services;
    }

    internal static IServiceCollection AddScoreboardServiceSingleton(
        this IServiceCollection services
    )
    {
        services.AddSingleton<ScoreboardService>();
        return services;
    }

    internal static IServiceCollection AddWaifuRollServices(this IServiceCollection services)
    {
        services.AddSingleton<WaifuRollService>();
        services.AddSingleton<WaifuRollEnsurenceService>();
        services.AddSingleton<WaifuPrizesService>();
        services.AddSingleton<IWaifuRollGuaranteeService, WaifuRollGuaranteeService>();

        // Фоновый запуск WaifuRollService
        services.AddHostedService(sp => sp.GetRequiredService<WaifuRollService>());

        return services;
    }

    internal static IServiceCollection AddRandomMemServices(this IServiceCollection services)
    {
        services.AddSingleton<RandomMemHandler>();
        services.AddSingleton<RandomMemeWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomMemeWorker>());
        services.AddSingleton<RandomMemOnline>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomMemOnline>());

        services.AddScoped<IRandomMemeService, RandomMemeService>();
        return services;
    }

    internal static IServiceCollection AddSyntheziaServices(this IServiceCollection services)
    {
        services.AddSingleton(
            (sp) => VoicerFactory.CreateVoicer(sp.GetRequiredService<ILogger<IVoicer>>())
        );
        services.AddSingleton<SyntheziaQueueManager>();
        services.AddHostedService(sp => sp.GetRequiredService<SyntheziaQueueManager>());
        return services;
    }

    internal static IServiceCollection Add365Services(this IServiceCollection services)
    {
        services.AddSingleton<Worker365>();
        services.AddHostedService(sp => sp.GetRequiredService<Worker365>());
        return services;
    }

    internal static IServiceCollection AddBooruServices(this IServiceCollection services)
    {
        services.AddSingleton<Gelbooru>(sp =>
        {
            var booruConfiguration =
                sp.GetService<IOptions<BooruConfiguration>>() ?? throw new NullReferenceException();

            return new Gelbooru
            {
                Auth = new BooruAuth(
                    booruConfiguration.Value.UserId,
                    booruConfiguration.Value.PwdHash
                ),
            };
        });

        return services;
    }

    /// <summary>
    /// Добавляет все Twitch-связанные сервисы
    /// </summary>
    internal static IServiceCollection AddTwitchServices(
        this IServiceCollection services,
        IConfigurationManager configuration
    )
    {
        services.AddTwitchEvents(configuration).AddTwitchStreamManagementServiceOnly();

        return services;
    }

    /// <summary>
    /// Добавляет все игровые сервисы
    /// </summary>
    internal static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services
            .AddHonkaiServices()
            .AddWaifuRollServices()
            .AddRandomMemServices()
            .AddScoreboardServiceSingleton();

        return services;
    }

    /// <summary>
    /// Добавляет все внешние API сервисы
    /// </summary>
    internal static IServiceCollection AddExternalApiServices(this IServiceCollection services)
    {
        services.AddShikimoriServices().AddPyroAlertsServices().AddBooruServices();

        return services;
    }

    /// <summary>
    /// Добавляет все специализированные сервисы
    /// </summary>
    internal static IServiceCollection AddSpecializedServices(this IServiceCollection services)
    {
        services.AddSyntheziaServices().Add365Services();

        return services;
    }
}
