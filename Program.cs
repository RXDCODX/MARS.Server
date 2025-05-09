using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BooruSharp.Booru;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.Services._365Genius;
using MARS.Server.Services.Honkai;
using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.AuthCodeService;
using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services.TelegramBotService.Commands;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using NJsonSchema.Generation;
using WTelegram;

namespace MARS.Server;

public class WTelegramClient(int item1, string item2, string item3) : Client(item1, item2, item3);

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var services = builder.Services;
        var configuration = builder.Configuration;

        var directory = AppDomain.CurrentDomain.BaseDirectory;

        var isWithSpa =
            builder.Environment.IsProduction() != true
            && Environment.GetEnvironmentVariable("ASPNETCORE_SPA_LAUNCH") is "TRUE";

        var contextFactory = new AppDbContextFactory(
            builder.Environment,
            builder.Configuration,
            options =>
            {
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
                options.EnableThreadSafetyChecks();
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();

                    options.UseNpgsql(configuration.GetConnectionString("Dev_Path"));
                }
                else
                {
                    options.UseNpgsql(configuration.GetConnectionString("Prod_Path"));
                }
            }
        );
        StaticDbContextFactory.Factory = contextFactory;

        //Twitch
        var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            if (builder.Environment.IsDevelopment())
            {
                //loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
            }
            else
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
            }

            var telegramConfiguration = new TelegramConfiguration();
            configuration
                .GetSection(AppBase.Base)
                .GetSection(TelegramConfiguration.TelegramSection)
                .Bind(telegramConfiguration);
            var botConfiguration = new BotConfiguration();
            configuration
                .GetSection(AppBase.Base)
                .GetSection(TelegramConfiguration.TelegramSection)
                .GetSection(BotConfiguration.Configuration)
                .Bind(botConfiguration);

            loggingBuilder.AddTelegramLogger(options =>
            {
                options.BotToken = botConfiguration.BotToken;
                options.ChatId = telegramConfiguration.AdminIdsArray;
                options.SourceName = "BOT";
                options.MinimumLevel = LogLevel.Warning;
            });
        });

        if (builder.Environment.IsProduction() && OperatingSystem.IsWindows())
        {
            var curPath = Directory.GetCurrentDirectory();
            var servicePath = curPath.Contains("C:")
                ? Environment.GetEnvironmentVariable(
                    "ZYZ_SERVICE_PATH",
                    EnvironmentVariableTarget.Machine
                )
                : curPath;
            if (string.IsNullOrWhiteSpace(servicePath))
            {
                throw new NullReferenceException();
            }

            directory = servicePath;
            Environment.CurrentDirectory = directory;
        }

        /////////////////////////////////////////////////////////////////////////////////////////

        services.AddSingleton<IDbContextFactory<AppDbContext>>(contextFactory);

        services.AddWindowsService(options =>
        {
            options.ServiceName = "!Zyz";
        });

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

        services.AddSingleton<PyroAlertsHelper>();
        services.AddSingleton<PyroAlertsHandler>();

        services.AddSingleton<DailyMarkMarkNotificationsSerivce>();
        services.AddHostedService(sp => sp.GetRequiredService<DailyMarkMarkNotificationsSerivce>());

        services.AddSingleton<AnswersForTwitchRewards>();

        services.AddSingleton<ShikimoriAuthorizationHelpService>();
        services.AddSingleton<ShikimoriService>();

        services.AddSingleton<WaifuRollService>();
        services.AddSingleton<WaifuRollDataBaseHelper>();

        services.AddSingleton<RandomMemHandler>();
        services.AddSingleton<RandomMemeWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomMemeWorker>());
        services.AddSingleton<RandomMemOnline>();
        services.AddHostedService(sp => sp.GetRequiredService<RandomMemOnline>());

        services.AddSingleton(
            (sp) => VoicerFactory.CreateVoicer(sp.GetRequiredService<ILogger<IVoicer>>())
        );
        services.AddSingleton<SyntheziaQueueManager>();
        services.AddHostedService(sp => sp.GetRequiredService<SyntheziaQueueManager>());

        services.AddSingleton<Worker365>();
        services.AddHostedService(sp => sp.GetRequiredService<Worker365>());

        services.AddSingleton<WTelegramClient>(
            (sp) =>
            {
                var options = sp.GetRequiredService<IOptions<WTelegramClientConfiguration>>().Value;
                var client = new WTelegramClient(
                    options.AppId,
                    options.ApiHash,
                    "bin/WTelegram.session"
                );
                var logger = loggerFactory.CreateLogger("WTelegram");
                Helpers.Log = (i, v) => logger.Log((LogLevel)i, v);
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

        services.AddSingleton<Gelbooru>(sp =>
        {
            var booruConfiguration =
                sp.GetService<IOptions<BooruConfiguration>>() ?? throw new NullReferenceException();

            return new Gelbooru()
            {
                Auth = new BooruAuth(
                    booruConfiguration.Value.UserId,
                    booruConfiguration.Value.PwdHash
                ),
            };
        });

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

        services.AddTwitchEvents(configuration, loggerFactory);

        services.AddScoped<Commands>();
        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();
        services.AddSingleton(loggerFactory);

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

        services.AddSwaggerGen(options =>
        {
            options.AddSignalRSwaggerGen();
            options.UseAllOfToExtendReferenceSchemas();
            options.UseAllOfForInheritance();
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

        var app = builder.Build();

        app.AddStaticFilesBrowser(directory, isWithSpa);
        app.UseSwagger();
        app.UseSwaggerUi(settings =>
        {
            settings.Path = "/ui";
            settings.DocumentPath = isWithSpa
                ? "/backend/swagger/{documentName}/swagger.json"
                : "/swagger/{documentName}/swagger.json";
            settings.DocumentTitle = "SWAGGER SCHEMA";
        });

        app.UseCors("CorsPolicy");
        app.MapHub<TelegramusHub>("/telegramus");
        app.MapHub<SoundBarHub>("/soundbar");

        app.UseRouting();

        app.MapControllers();

        if (isWithSpa)
        {
            app.MapFallbackToFile("index.html");
        }

        var cp = Process.GetCurrentProcess();
        cp.PriorityClass = ProcessPriorityClass.RealTime;

        var appLifeTime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        appLifeTime.ApplicationStopping.Register(MemoryStorage.ClearStorage);

        app.Run();
    }
}
