global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
global using MARS.Server.Configuration;
global using MARS.Server.DataBaseContext;
global using MARS.Server.Exstensions;
global using MARS.Server.Hubs;
global using MARS.Server.Hubs.Interfaces;
global using MARS.Server.Services.PyroAlerts.Entitys;
global using MARS.Server.Services.Telegram.BotService.Entitys;
global using MARS.Server.Services.WaifuRoll.Entitys;
global using Microsoft.AspNetCore.SignalR;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Options;
global using Telegram.Bot;
global using Telegram.Bot.Types;
global using TwitchLib.Api.Interfaces;
global using TwitchLib.Client.Interfaces;
global using Color = System.Drawing.Color;
global using Host = MARS.Server.Services.WaifuRoll.Entitys.Host;
global using Timer = System.Timers.Timer;
global using WTelegramClient = WTelegram.Client;
using System.Diagnostics;
using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.CustomLoggers.SignalRLogger;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.Services.CinemaQueue;
using MARS.Server.Services.CommandExecutor;
using MARS.Server.Services.Configuration;
using MARS.Server.Services.KeyboardHook_UNUSED;
using MARS.Server.Services.Logs.Interfaces;
using MARS.Server.Services.Logs.Services;
using MARS.Server.Services.Media;
using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Rewards;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace MARS.Server;

public static class Program
{
    public static bool IsUseSoundRequest { get; set; }
    public static bool IsUseSwagger { get; set; }
    private const string GenerateOpenApiArgument = "--generate-openapi";

    public static async Task Main(string[] args)
    {
        var shouldGenerateOpenApi = args.Any(arg =>
            arg.Equals(GenerateOpenApiArgument, StringComparison.OrdinalIgnoreCase)
        );

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

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
                if (OperatingSystem.IsWindows())
                {
                    loggingBuilder.AddEventLog();
                }
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
            loggingBuilder.AddDbLogger(() =>
            {
                var options = new DbContextOptionsBuilder<LoggerDbContext>();

                return new DbLoggerOptions
                {
                    Factory = new LoggerDbContextFactory(
                        (contextOptionsBuilder) =>
                        {
                            contextOptionsBuilder.UseQueryTrackingBehavior(
                                QueryTrackingBehavior.NoTracking
                            );
                            contextOptionsBuilder.EnableThreadSafetyChecks();

                            if (builder.Environment.IsDevelopment())
                            {
                                contextOptionsBuilder.UseNpgsql(
                                    configuration.GetConnectionString("Dev_Path")
                                );
                                contextOptionsBuilder.EnableDetailedErrors();
                                contextOptionsBuilder.EnableSensitiveDataLogging();
                            }
                            else
                            {
                                contextOptionsBuilder.UseNpgsql(
                                    configuration.GetConnectionString("Prod_Path")
                                );
                            }
                        }
                    ),
                    MinimumLogLevel = builder.Environment.IsProduction()
                        ? LogLevel.Warning
                        : LogLevel.Information,
                    Environment = builder.Environment,
                };
            });

            // Добавляем SignalR логгер
            loggingBuilder.AddSignalRLogger(options =>
            {
                options.MinimumLogLevel = builder.Environment.IsProduction()
                    ? LogLevel.Warning
                    : LogLevel.Information;
                options.SourceName = "MARS.Server";
                options.IncludeExceptions = true;
                options.IncludeStackTrace = true;
                options.MaxMessageLength = 2000;

                // Исключаем некоторые категории для уменьшения шума
                options.ExcludedCategories =
                [
                    "Microsoft.AspNetCore.Hosting.Diagnostics",
                    "Microsoft.AspNetCore.Routing.EndpointMiddleware",
                    "Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware",
                    "AspNetCore.SpaYarp.SpaProxyMiddleware",
                ];
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

        services
            .AddTwitchServices(configuration)
            .AddHostedService<TwitchUserSyncService>()
            .AddCommandExecutorServices()
            .AddTelegramThings(loggerFactory)
            .AddConfiguration(configuration)
            .AddBaseAspNetMiddlewares(configuration)
            .AddSwaggerServices()
            .AddCinemaQueueServicesAsSingleton()
            .AddGameServices()
            .AddExternalApiServices()
            .AddSpecializedServices()
            .AddSoundRequest();

        // Media file storage
        services.AddSingleton<IMediaFileStorageService, WebRootMediaFileStorageService>();
        services.AddSingleton<IMediaInspector, FfprobeMediaInspector>();
        services.AddSingleton<IMediaTranscoder, MediaTranscoder>();

        services.AddSingleton<IDbContextFactory<AppDbContext>>(contextFactory);
        services.AddHostedService<ConfigurationKeysBootstrapHostedService>();

        services.AddWindowsService(options =>
        {
            options.ServiceName = "!Zyz";
        });

        services.AddSingleton<AnswersForTwitchRewards>();

        // WTelegramClient registration moved to StartupExtensions.AddTelegramThings()

        // Добавляем сервис архивирования потоков
        //services.AddSingleton<IFFmpegService, FFmpegService>();
        //services.AddSingleton<IStreamArchiveService, StreamArchiveService>();
        //services.AddHostedService<StreamArchiveWorker>();

        services.AddSingleton(loggerFactory);

        // Добавляем сервис перехвата клавиатуры
        services.AddKeyboardHookService();

        // Добавляем сервис для работы с логами
        services.AddScoped<LoggerDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<LoggerDbContext>();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableThreadSafetyChecks();

            if (builder.Environment.IsDevelopment())
            {
                options.UseNpgsql(configuration.GetConnectionString("Dev_Path"));
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
            else
            {
                options.UseNpgsql(configuration.GetConnectionString("Prod_Path"));
            }

            return new LoggerDbContext(options.Options);
        });
        services.AddScoped<ILogsService, LogsService>();
        services.AddMvc();

        builder.AddStaticFilesBrowserOptions();

        var app = builder.Build();
        var logger = app.Logger;

        if (shouldGenerateOpenApi)
        {
            await GenerateOpenApiFilesAsync(app);
            return;
        }

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

        app.UseCors("CorsPolicy");
        app.MapHub<TelegramusHub>("/hubs/telegramus");
        app.MapHub<TunaHub>("/hubs/tuna");
        //app.MapHub<SoundBarHub>("/hubs/soundbar");
        app.MapHub<ScoreboardHub>("/hubs/scoreboard");
        app.MapHub<VoiceRecognitionHub>("/hubs/tts");
        app.AddLogerHub();
        if (IsUseSoundRequest)
        {
            app.MapHub<SoundRequestHub>("/hubs/soundrequest");
        }

        app.UseRouting();

        app.MapControllers();

        if (isSpa)
        {
            app.UseSpaYarp();
        }
        else
        {
            app.MapFallbackToFile("index.html");
        }

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

    private static async Task GenerateOpenApiFilesAsync(WebApplication app)
    {
        var outputDirectory = Path.GetFullPath(
            Path.Combine(app.Environment.ContentRootPath, "..", "mars.client", "api")
        );

        Directory.CreateDirectory(outputDirectory);

        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var apiDocument = swaggerProvider.GetSwagger("api");
        var hubsDocument = swaggerProvider.GetSwagger("hubs");

        await WriteSwaggerDocumentAsync(
            Path.Combine(outputDirectory, "swagger_api.json"),
            apiDocument
        );
        await WriteSwaggerDocumentAsync(
            Path.Combine(outputDirectory, "swagger_hubs.json"),
            hubsDocument
        );

        Console.WriteLine(
            $"OpenAPI schema generated: {Path.Combine(outputDirectory, "swagger_api.json")}"
        );
        Console.WriteLine(
            $"OpenAPI schema generated: {Path.Combine(outputDirectory, "swagger_hubs.json")}"
        );
    }

    private static async Task WriteSwaggerDocumentAsync(string filePath, OpenApiDocument document)
    {
        await using var stream = File.Create(filePath);
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        var jsonWriter = new OpenApiJsonWriter(writer);

        document.SerializeAsV3(jsonWriter);
        await writer.FlushAsync();
    }
}
