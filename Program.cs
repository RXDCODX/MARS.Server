using System.Diagnostics;
using BooruSharp.Booru;
using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.DataBaseContext;
using MARS.Server.Services._365Genius;
using MARS.Server.Services.CommandExecutor;
using MARS.Server.Services.Honkai;
using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.PyroAlerts;
using MARS.Server.Services.RandomMem;
using MARS.Server.Services.Scoreboard;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Twitch.Rewards;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.helpers;
using WTelegram;

namespace MARS.Server;

public class WTelegramClient(int item1, string item2, string item3) : Client(item1, item2, item3);

public static class Program
{
    public static bool IsUseSoundRequest { get; set; }
    public static bool IsUseSwagger { get; set; }

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var services = builder.Services;
        var configuration = builder.Configuration;

        var directory = AppDomain.CurrentDomain.BaseDirectory;

        var isSpa =
            !builder.Environment.IsProduction()
            && Convert.ToBoolean(
                Environment.GetEnvironmentVariable("ASPNETCORE_SPA_LAUNCH") ?? string.Empty
            );

        var contextFactory = new AppDbContextFactory(options =>
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
        });
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

        await services.AddTwitchEvents(configuration);
        services.AddCommandExecutorServices();
        services.AddTelegramThings(loggerFactory);
        services.AddConfiguration(configuration);
        //services.AddYandexMusic();
        //services.AddSoundRequest();
        services.AddBaseAspNetMiddlewares();
        services.AddSwaggerServices();
        services.AddHonkaiServices();

        if (builder.Environment.IsProduction())
        {
            builder.Logging.AddDbLogger(() =>
            {
                var options = new DbContextOptionsBuilder<LoggerDbContext>();
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableThreadSafetyChecks();
                options.UseNpgsql(configuration.GetConnectionString("Prod_Path"));

                return new DbLoggerOptions { DbContext = new LoggerDbContext(options.Options) };
            });
        }

        services.AddSingleton<IDbContextFactory<AppDbContext>>(contextFactory);

        services.AddWindowsService(options =>
        {
            options.ServiceName = "!Zyz";
        });

        services.AddSingleton<PyroAlertsHelper>();
        services.AddSingleton<PyroAlertsHandler>();

        services.AddSingleton<AnswersForTwitchRewards>();

        services.AddSingleton<ShikimoriService>();

        services.AddSingleton<ScoreboardService>();

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

        services.AddSingleton<WaifuRollWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<WaifuRollWorker>());

        services.AddSingleton(loggerFactory);

        // Добавляем инициализатор базы данных
        services.AddDataBaseInitializer();

        builder.AddStaticFilesBrowserOptions();

        var app = builder.Build();
        var logger = app.Logger;

        app.AddStaticFilesBrowser();

        if (IsUseSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = "ui";
                c.SwaggerEndpoint("/swagger/api/swagger.json", "API");
                c.SwaggerEndpoint("/swagger/hubs/swagger.json", "Hubs");
                c.DocumentTitle = "SWAGGER SCHEMA";
            });
        }

        if (isSpa)
        {
            app.UseSpaYarp();
        }

        app.UseCors("CorsPolicy");
        app.MapHub<TelegramusHub>("/hubs/telegramus");
        app.MapHub<TunaHub>("/hubs/tuna");
        //app.MapHub<SoundBarHub>("/hubs/soundbar");
        app.MapHub<ScoreboardHub>("/hubs/scoreboard");
        if (IsUseSoundRequest)
        {
            app.MapHub<SoundRequestHub>("/hubs/soundrequest");
        }

        app.UseRouting();

        app.MapControllers();

        app.MapFallbackToFile("index.html");

        try
        {
            var cp = Process.GetCurrentProcess();
            cp.PriorityClass = ProcessPriorityClass.RealTime;

            var appLifeTime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            appLifeTime.ApplicationStopping.Register(MemoryStorage.ClearStorage);

            await app.RunAsync();
        }
        catch (Exception e)
        {
            logger.LogException(e);
        }
    }
}
