using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.Configuration;
using MARS.Server.CustomLoggers.SignalRLogger;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Filters;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Migrations;
using MARS.Server.Services._365Genius;
using MARS.Server.Services.BooruShared;
using MARS.Server.Services.DanbooruAutoPost;
using MARS.Server.Services.NSFWBooru;
using MARS.Server.Services.Discord.Gateway;
using MARS.Server.Services.Discord.PlayRequest;
using MARS.Server.Services.Discord.TtsVoiceRelay;
using MARS.Server.Services.Obs;
using MARS.Server.Services.AudioControllerHub;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.Scoreboard;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.SoundBarService;
using MARS.Server.Services.SoundBarService.Entitys;
using MARS.Server.Services.SoundRequest;
using MARS.Server.Services.SoundRequest.Interfaces;
using MARS.Server.Services.SoundRequest.Queue;
using MARS.Server.Services.SoundRequest.SoundCloud;
using MARS.Server.Services.SoundRequest.Spotify;
using MARS.Server.Services.Telegram.BotService;
using MARS.Server.Services.Telegram.ClipboardCopy;
using MARS.Server.Services.Telegram.DiscordBridge;
using MARS.Server.Services.Telegram.GooglePhotos;
using MARS.Server.Services.Telegram.PrivateChannelsResender;
using MARS.Server.Services.Telegram.WTelegram;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.AutoInfoFetch;
using MARS.Server.Services.Twitch.BlackList;
using MARS.Server.Services.Twitch.Client;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Extensions;
using MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Media;
using MARS.Server.Services.Twitch.MiniGamesStats;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;
using MARS.Server.Services.Twitch.Rewards._13_FumoFriday;
using MARS.Server.Services.Twitch.Rewards._1580_MikuBeam;
using MARS.Server.Services.Twitch.Rewards._160_LegBum;
using MARS.Server.Services.Twitch.Rewards._2_WaifuMarriage;
using MARS.Server.Services.Twitch.Rewards._27_RandomArt;
using MARS.Server.Services.Twitch.Rewards._39_MikuMonday;
using MARS.Server.Services.Twitch.Rewards._4_FrogRoll;
using MARS.Server.Services.Twitch.Rewards._4_FumoRoll;
using MARS.Server.Services.Twitch.Rewards._4_MikuRoll;
using MARS.Server.Services.Twitch.Rewards._5_AddWife;
using MARS.Server.Services.Twitch.Rewards._6_RussianRoulette;
using MARS.Server.Services.Twitch.Rewards._7_Quiz;
using MARS.Server.Services.Twitch.Rewards._9_AudioQuiz;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using MARS.Server.Services.Twitch.StreamManagement;
using MARS.Server.Services.SevenTv;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.TwitchFollowers;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.Twitch.WeddingAnniversary;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.Entitys.Interfaces;
using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Services.YouTube;
using MARS.Server.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Telegram.Bot;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.HttpCallHandlers;
using TwitchLib.Api.Core.Interfaces;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchLib.EventSub.Websockets.Extensions;
using RandomMemOnline = MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.RandomMemOnline;

namespace MARS.Server;

public static class StartupEstensions
{
    internal static IServiceCollection AddBaseAspNetMiddlewares(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSpaYarp();

        var aiConnectionString =
            configuration["ApplicationInsights:ConnectionString"]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var aiInstrumentationKey =
            configuration["ApplicationInsights:InstrumentationKey"]
            ?? configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

        if (
            !string.IsNullOrWhiteSpace(aiConnectionString)
            || !string.IsNullOrWhiteSpace(aiInstrumentationKey)
        )
        {
            services.AddApplicationInsightsTelemetry();
        }

        services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024 * 1024;
                options.AddFilter<LoggerHubRecursionFilter>();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddSingleton<LoggerHubRecursionGuard>();

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
                    builder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
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

        // Регистрируем сервис-обертку для WTelegram как Singleton
        // UpdateHandler получит его через IServiceProvider
        services.AddSingleton<WTelegramClientService>();
        services.AddSingleton<TelegramClipboardCopyService>();

        // Регистрируем сервисы Google Photos
        services.AddSingleton<GooglePhotosAuthService>();
        services.AddSingleton<GooglePhotosApiClient>();
        services.AddSingleton<TelegramGooglePhotosService>();

        // Для обратной совместимости регистрируем также WTelegram.Client
        services.AddSingleton<WTelegram.Client>(sp =>
        {
            try
            {
                var clientService = sp.GetRequiredService<WTelegramClientService>();
                return clientService.GetClientAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var logger = sp.GetService<ILogger<WTelegramClientService>>();
                logger?.LogWarning(ex, "WTelegram клиент не удалось инициализировать");
                return null!;
            }
        });

        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();

        // Регистрируем сервис для пересылки медиа из forwarded сообщений
        services.AddHostedService<TelegramChannelsResenderService>();

        services.AddSingleton<IMediaCompressor, MediaCompressor>();

        services.AddSingleton<DiscordGatewayService>();
        services.AddSingleton<IDiscordGatewayService>(sp =>
            sp.GetRequiredService<DiscordGatewayService>()
        );
        services.AddHostedService<DiscordGatewayService>(sp =>
            sp.GetRequiredService<DiscordGatewayService>()
        );

        services.AddSingleton<DiscordTtsVoiceRelayService>();
        services.AddSingleton<IDiscordTtsVoiceRelayService>(sp =>
            sp.GetRequiredService<DiscordTtsVoiceRelayService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<DiscordTtsVoiceRelayService>());

        services.AddSingleton<TelegramDiscordBridgeService>();
        services.AddSingleton<ITelegramDiscordBridgeService>(sp =>
            sp.GetRequiredService<TelegramDiscordBridgeService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<TelegramDiscordBridgeService>());

        // Регистрируем ScoreboardService
        services.AddScoped<ScoreboardService>();

        services.AddSingleton<HelloVideoWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<HelloVideoWorker>());

        services.AddSingleton<AutoMessagesHandler>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoMessagesHandler>());

        return services;
    }

    internal static IServiceCollection AddTwitchEvents(
        this IServiceCollection services,
        IConfigurationManager manager
    )
    {
        // Регистрируем сервис управления наградами и кеш наград перед инициализацией временных наград
        services.AddChannelRewardsManager();
        services.InitializeTwitchRewards();

        services.AddSingleton<TelegramTokenNotification>();
        services.AddSingleton<TokenService>();
        services.AddHostedService(sp => sp.GetRequiredService<TokenService>());
        services.AddSingleton<EventSubService>();
        services.AddHostedService(sp => sp.GetRequiredService<EventSubService>());

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
                new ApiSettings { ClientId = twitchConfig.ClientId },
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

        services.AddSingleton<ITwitchEventValidationService, TwitchEventValidationService>();

        services.AddSingleton<MiniGamesManager>();
        services.AddHostedService(sp => sp.GetRequiredService<MiniGamesManager>());

        services.AddSingleton<YouTubeResolver>();
        services.AddSingleton<MikuMondayTracksService>();

        services.AddSingleton<TwitchMikuBeamRewardService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMikuBeamRewardService>());

        services.AddHostedService<LegBumRefundService>();
        services.AddSingleton<ServiceManager>();
        services.AddSingleton<IServiceManager>(sp => sp.GetRequiredService<ServiceManager>());

        // Регистрируем сервис для работы с зрителями канала rxdcodx
        services.AddRxdcodxViewersServiceAsSingleton();

        // Регистрируем сервис для работы с автоматическими сообщениями
        services.AddAutoMessagesService();

        // Регистрируем сервисы для работы с пользователями Twitch
        // Singleton безопасен, т.к. сервис использует IDbContextFactory и не хранит состояние
        services.AddSingleton<TwitchUserEnsureService>();
        services.AddSingleton<ITwitchUserEnsureService>(sp =>
            sp.GetRequiredService<TwitchUserEnsureService>()
        );

        services.AddSingleton<RickRollerService>();

        services.AddSingleton<TwitchBlackListService>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchBlackListService>());

        services.AddSingleton<TwitchConnectionManager>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchConnectionManager>());
        services.AddSingleton<ITwitchClient>(sp =>
            sp.GetRequiredService<TwitchConnectionManager>().Proxy
        );

        services.AddTwitchLibEventSubWebsockets();
        services.AddHostedService<EventSubService>();

        services.AddSingleton<TwitchStreamStartupNotifications>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchStreamStartupNotifications>());
        services.AddSingleton<TwitchMediaAlerts>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMediaAlerts>());
        services.AddSingleton<AutoMessagesHandler>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoMessagesHandler>());

        // Регистрируем сервис для работы с шаблонами сообщений Twitch
        // services.AddSingleton<TwitchMessageBuilderService>();
        // services.AddHostedService(sp => sp.GetRequiredService<TwitchMessageBuilderService>());

        services.AddSingleton<AutoHello>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoHello>());

        services.AddSingleton<AddNewWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<AddNewWaifu>());
        services.AddSingleton<MergeWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<MergeWaifu>());
        services.AddSingleton<RollCooldownNotificationService>();
        services.AddHostedService(sp => sp.GetRequiredService<RollCooldownNotificationService>());
        services.AddScoped<TwitchRussianRoulete>();
        services.AddScoped<TekkenVictorina>();
        services.AddScoped<TwitchTrivia>();
        services.AddScoped<AudioTriviaMiniGame>();
        services.AddSingleton<PuntoSwitcherService>();
        services.AddSingleton<IPuntoSwitcherService>(sp =>
            sp.GetRequiredService<PuntoSwitcherService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<PuntoSwitcherService>());
        services.AddSingleton<HighlitedMessage>();
        services.AddHostedService(sp => sp.GetRequiredService<HighlitedMessage>());
        services.AddSingleton<HelloVideoWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<HelloVideoWorker>());

        services.AddSingleton<WeddingAnniversaryService>();

        services.AddSingleton<RandomArt>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomArt>());

        services.AddSingleton<TwitchMessagesHubAwaker>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchMessagesHubAwaker>());

        services.AddSingleton<SoundMuteCoordinator>();

        services.AddSingleton<AutoRewardInfoFetcher>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoRewardInfoFetcher>());

        // Настройки временных наград: словарь Cost -> enabled
        services.Configure<TwitchRewardsOptions>(
            manager.GetSection(AppBase.Base).GetSection(TwitchRewardsOptions.SectionName)
        );

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
        services.Configure<ObsConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(ObsConfiguration.SectionName)
        );
        services.Configure<SoundRequestConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(SoundRequestConfiguration.SectionName)
        );
        services.Configure<SpotifySoundRequestConfiguration>(
            configuration
                .GetSection(AppBase.Base)
                .GetSection(SpotifySoundRequestConfiguration.SectionName)
        );
        services.Configure<GooglePhotosConfiguration>(
            configuration.GetSection(AppBase.Base).GetSection(GooglePhotosConfiguration.SectionName)
        );

        return services;
    }

    public static IServiceCollection AddSoundRequest(this IServiceCollection services)
    {
        Program.IsUseSoundRequest = true;

        // Регистрируем базовые сервисы
        services.AddSingleton<StateManager>();
        services.AddSingleton<InSignalRHubService>();
        services.AddSingleton<OutSignalRHubService>();
        services.AddSingleton<SoundRequestUserQueue>();
        services.AddSingleton<SoundMuteCoordinator>();

        // YouTube / Discord play request services
        services.AddSingleton<DiscordPlayAudioCacheService>();
        services.AddSingleton<DiscordPlayRequestService>();
        services.AddHostedService(sp => sp.GetRequiredService<DiscordPlayRequestService>());

        // Spotify services
        services.AddSingleton<SpotifyAuthService>();
        services.AddHttpClient<SpotifyApiClient>();
        services.AddSingleton<SpotifyResolver>();
        services.AddSingleton<SpotifyPlaybackService>();
        services.AddSingleton<SoundCloudResolver>();

        // Регистрируем плеер и SoundRequestCommandsService
        services.AddSingleton<MainPlayer>();
        services.AddSingleton<IPlayerController>(sp => sp.GetRequiredService<MainPlayer>());
        services.AddHostedService(sp => sp.GetRequiredService<MainPlayer>());
        services.AddSingleton<SoundRequestCommandsService>();

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
            options.DocumentFilter<OperationResultSchemaFilter>(); // Опционально: оптимизация OperationResult (пока не используется)

            // Two separate documents served by Swashbuckle
            options.SwaggerDoc("api", new OpenApiInfo { Title = "Telegramus API" });
            options.SwaggerDoc("hubs", new OpenApiInfo { Title = "Telegramus Hubs" });
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

    extension(IServiceCollection services)
    {
        internal IServiceCollection AddPyroAlertsServices()
        {
            services.AddSingleton<PyroAlertsHelper>();
            services.AddSingleton<PyroAlertsHandler>();
            return services;
        }

        internal IServiceCollection AddShikimoriServices()
        {
            services.AddSingleton<IShikimoriRateLimiter, ShikimoriShikimoriRateLimiter>();
            services.AddSingleton<ShikimoriService>();
            return services;
        }

        internal IServiceCollection AddScoreboardServiceSingleton()
        {
            services.AddSingleton<ScoreboardService>();
            return services;
        }

        internal IServiceCollection AddWaifuRollServices()
        {
            services.AddSingleton<WaifuRollService>();
            services.AddSingleton<WaifuRollEnsurenceService>();
            services.AddSingleton<WaifuPrizesService>();
            services.AddSingleton<IWaifuRollGuaranteeService, WaifuRollGuaranteeService>();

            services.AddSingleton<MergeWaifu>();
            services.AddHostedService(sp => sp.GetRequiredService<MergeWaifu>());

            // Fumo Friday services
            // Roll cooldown service (shared by Miku/Fumo/Frog)
            services.AddSingleton<RollCooldownService>();

            services.AddSingleton<FumoRollService>();
            services.AddSingleton<FumoCollectionService>();

            // Frog services
            services.AddSingleton<FrogRollService>();

            // Miku Module Roll services
            services.AddSingleton<MikuRollService>();
            services.AddSingleton<MikuCollectionService>();

            // Фоновый запуск WaifuRollService
            services.AddHostedService(sp => sp.GetRequiredService<WaifuRollService>());

            services.AddSingleton<WeddingAnniversaryService>();

            return services;
        }

        internal IServiceCollection AddRandomMemServices()
        {
            services.AddSingleton<ITwitchMediaPreparationService, TwitchMediaPreparationService>();
            services.AddSingleton<TwitchMediaTranscodeWorker>();
            services.AddHostedService(sp => sp.GetRequiredService<TwitchMediaTranscodeWorker>());
            services.AddSingleton<RandomMemHandler>();
            services.AddSingleton<RandomMemeWorker>();
            services.AddHostedService(sp => sp.GetRequiredService<RandomMemeWorker>());
            services.AddSingleton<RandomMemOnline>();
            services.AddHostedService(sp => sp.GetRequiredService<RandomMemOnline>());

            services.AddScoped<IRandomMemeService, RandomMemeService>();
            return services;
        }

        internal IServiceCollection AddSyntheziaServices()
        {
            services.AddHttpClient("SevenTv");
            services.AddSingleton<ISevenTvApiService, SevenTvApiService>();
            services.AddSingleton<ISevenTvEmoteService, SevenTvEmoteService>();
            services.AddHostedService(sp =>
                (SevenTvEmoteService)sp.GetRequiredService<ISevenTvEmoteService>()
            );
            services.AddSingleton<TtsHubBroadcaster>();
            services.AddSingleton<ITtsHubBroadcaster>(sp =>
                sp.GetRequiredService<TtsHubBroadcaster>()
            );
            services.AddHostedService(sp => sp.GetRequiredService<TtsHubBroadcaster>());
            services.AddSingleton<ITtsMessageFilterService, TtsMessageFilterService>();
            return services;
        }

        internal IServiceCollection Add365Services()
        {
            services.AddSingleton<IDnsResolver, SystemDnsResolver>();
            services.AddSingleton<SiteAvailabilityChecker>();
            services.AddSingleton<SiteUnavailableNotifier>();
            services.AddSingleton<Worker365>();
            services.AddHostedService(sp => sp.GetRequiredService<Worker365>());
            return services;
        }

        internal IServiceCollection AddBooruServices()
        {
            services.AddSingleton<DanbooruRandomPostService>();
            services.AddSingleton<DanbooruAutoPostService>();
            services.AddSingleton<IDanbooruAutoPostService>(sp =>
                sp.GetRequiredService<DanbooruAutoPostService>()
            );
            services.AddHostedService(sp => sp.GetRequiredService<DanbooruAutoPostService>());

            services.AddSingleton<IDeduplicationService, DeduplicationService>();

            services.AddSingleton<NSFWBooruRandomPostService>();
            services.AddSingleton<NsfwBooruAutoPostService>();
            services.AddSingleton<INSFWBooruAutoPostService>(sp =>
                sp.GetRequiredService<NsfwBooruAutoPostService>()
            );
            services.AddHostedService(sp => sp.GetRequiredService<NsfwBooruAutoPostService>());

            return services;
        }

        /// <summary>
        /// Добавляет все Twitch-связанная сервисы
        /// </summary>
        internal IServiceCollection AddTwitchServices(IConfigurationManager configuration)
        {
            services.AddTwitchEvents(configuration).AddTwitchStreamManagementServiceOnly();

            return services;
        }

        /// <summary>
        /// Добавляет все игровые сервисы
        /// </summary>
        internal IServiceCollection AddGameServices()
        {
            services
                //.AddHonkaiServices()

                .AddWaifuRollServices()
                .AddRandomMemServices()
                .AddScoreboardServiceSingleton();

            return services;
        }

        /// <summary>
        /// Добавляет все внешние API сервисы
        /// </summary>
        internal IServiceCollection AddExternalApiServices()
        {
            services.AddShikimoriServices().AddPyroAlertsServices().AddBooruServices();

            return services;
        }

        /// <summary>
        /// Добавляет все специализированные сервисы
        /// </summary>
        internal IServiceCollection AddSpecializedServices()
        {
            services.AddSyntheziaServices().Add365Services();

            return services;
        }

        internal IServiceCollection AddObsServices()
        {
            // IObsService is now registered via AddAudioControllerHubServices
            return services;
        }

        internal IServiceCollection AddAudioControllerHubServices()
        {
            services.AddSingleton<
                Hubs.AudioControllerHub.AudioControllerCommandTracker
            >();
            services.AddSingleton<SignalRAudioControllerService>();
            services.AddSingleton<ISoundBar>(sp =>
                sp.GetRequiredService<SignalRAudioControllerService>()
            );
            services.AddSingleton<IObsService>(sp =>
                sp.GetRequiredService<SignalRAudioControllerService>()
            );

            return services;
        }
    }
}
