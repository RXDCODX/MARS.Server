using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.Services.Honkai;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.AuthCodeService;
using MARS.Server.Services.TelegramBotService;
using MARS.Server.Services.TelegramBotService.Commands;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using NJsonSchema.Generation;

namespace MARS.Server;

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

        var dbConnectionString = builder.Environment.IsDevelopment()
            ? configuration.GetConnectionString("Dev_Path")
            : configuration.GetConnectionString("Prod_Path");

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
                }

                options.UseNpgsql(dbConnectionString);
            }
        );

        //Twitch
        var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            if (builder.Environment.IsDevelopment())
            {
                //loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
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
            var servicePath = Environment.GetEnvironmentVariable(
                "ZYZ_SERVICE_PATH",
                EnvironmentVariableTarget.Machine
            );
            if (string.IsNullOrWhiteSpace(servicePath))
            {
                throw new NullReferenceException();
            }

            directory = servicePath;
            Environment.CurrentDirectory = directory;
        }

        /////////////////////////////////////////////////////////////////////////////////////////


        JobStorage.Current = new PostgreSqlStorage(
            new NpgsqlConnectionFactory(dbConnectionString, new PostgreSqlStorageOptions())
        );
        services
            .AddHangfire(op =>
            {
                op.UsePostgreSqlStorage(
                    (bs) =>
                    {
                        bs.UseNpgsqlConnection(
                            builder.Environment.IsDevelopment()
                                ? configuration.GetConnectionString("Dev_Path")
                                : configuration.GetConnectionString("Prod_Path")
                        );
                    }
                );

                op.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
                op.UseSimpleAssemblyNameTypeSerializer();
                op.UseRecommendedSerializerSettings();
            })
            .AddHangfireServer();

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
        RecurringJob.AddOrUpdate<DailyMarkMarkNotificationsSerivce>(
            "daily-mark-up",
            x => x.NotifyAsync(CancellationToken.None),
            "0 */2 * * *"
        );

        services.AddSingleton<ShikimoriAuthorizationHelpService>();
        services.AddSingleton<ShikimoriService>();

        services.AddSingleton<WaifuRollService>();
        services.AddSingleton<WaifuRollDataBaseHelper>();

        services.AddSingleton<RandomMemHandler>();
        services.AddSingleton<RandomMemeWorker>();
        RecurringJob.AddOrUpdate<RandomMemeWorker>(
            "random-meme-worker",
            (x) => x.Process(CancellationToken.None),
            "*/30 * * * *"
        );

        services.AddSingleton(
            (sp) => VoicerFactory.CreateVoicer(sp.GetRequiredService<ILogger<IVoicer>>())
        );
        services.AddSingleton<SyntheziaQueueManager>();

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
                policyBuilder =>
                {
                    policyBuilder
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(host => true)
                        .AllowCredentials();
                }
            )
        );

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.AddStaticFilesBrowser(directory);
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

        app.UseRouting();

        app.MapControllers();

        app.MapHangfireDashboard();

        if (isWithSpa)
        {
            app.MapFallbackToFile("index.html");
        }

        var cp = Process.GetCurrentProcess();
        cp.PriorityClass = ProcessPriorityClass.RealTime;

        app.Run();
    }
}
