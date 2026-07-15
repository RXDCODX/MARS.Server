using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.CustomLoggers.SignalRLogger;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Services.CinemaQueue;
using MARS.Server.Services.CommandExecutor;
using MARS.Server.Services.Configuration;
using MARS.Server.Services.KeyboardHook_UNUSED;
using MARS.Server.Services.Logs.Interfaces;
using MARS.Server.Services.Logs.Services;
using MARS.Server.Services.Media;
using MARS.Server.Services.MemoryStorageService;
using MARS.Server.Services.Obs;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.Rewards;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using TL;

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

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { Args = args, ApplicationName = "MARS.Server" }
        );

        var services = builder.Services;
        var configuration = builder.Configuration;

        var isSpa =
            !builder.Environment.IsProduction()
            && bool.TryParse(
                Environment.GetEnvironmentVariable("ASPNETCORE_SPA_LAUNCH"),
                out var spaLaunch
            )
            && spaLaunch;

        var isStaging = builder.Environment.IsStaging();
        var useDevConnection = builder.Environment.IsDevelopment() || isStaging;

        //Twitch
        var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));

            if (builder.Environment.IsDevelopment())
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
            }
            else
            {
                loggingBuilder.AddConsole();
                if (OperatingSystem.IsWindows())
                {
                    loggingBuilder.AddEventSourceLogger();
                    loggingBuilder.AddEventLog();
                }
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
            }

            // Telegram логгер
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

            // DbLogger — только вне Staging
            if (!isStaging)
            {
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

                                if (useDevConnection)
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
            }

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

        builder.Services.Replace(new ServiceDescriptor(typeof(ILoggerFactory), loggerFactory));

        var contextFactory = new AppDbContextFactory(options =>
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
            options.EnableThreadSafetyChecks();
            options.UseLoggerFactory(loggerFactory);
            if (useDevConnection)
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

            Environment.CurrentDirectory = servicePath;
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
            .AddSoundRequest()
            .AddObsServices();

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

        // Добавляем сервис перехвата клавиатуры
        services.AddKeyboardHookService();

        // Добавляем сервис для работы с логами
        services.AddScoped<LoggerDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<LoggerDbContext>();
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableThreadSafetyChecks();

            if (useDevConnection)
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

        app.Map(
            "/allservices",
            builder =>
                builder.Run(async context =>
                {
                    var sb = new StringBuilder();
                    var dependencies = new Dictionary<string, string>();
                    sb.AppendLine("<pre>");
                    sb.AppendLine("digraph Services {");
                    var servicesDi = services.Select(svc => svc.ServiceType.ToString()).ToHashSet();

                    foreach (var svc in services)
                    {
                        var implementationName = svc.ImplementationType?.ToString();
                        if (implementationName != null)
                        {
                            var implDependencies = svc
                                .ImplementationType?.GetConstructors()
                                .SelectMany(cons => cons.GetParameters())
                                .Select(p => p.ParameterType.ToString())
                                .Distinct()
                                .Where(servicesDi.Contains)
                                .ToList();

                            if (implDependencies is { Count: > 0 })
                            {
                                // Register Constructor dependendencies
                                foreach (var d in implDependencies)
                                {
                                    dependencies.TryAdd(implementationName, d);
                                }
                            }
                        }
                    }

                    void PrintGroup(
                        string label,
                        string cluster,
                        string color,
                        IEnumerable<string> group
                    )
                    {
                        if (group.Count() > 0)
                        {
                            sb.AppendLine($"  subgraph cluster_{cluster} {{");
                            sb.AppendLine("      style = filled;");
                            sb.AppendLine($"     color = {color};");
                            sb.AppendLine("      node[style = filled, color = white];");
                            sb.AppendLine($"     label = \"{label}\";");
                            foreach (var item in group)
                            {
                                sb.AppendLine($"     \"{item}\"");
                            }

                            sb.AppendLine("  }");
                        }
                    }

                    var scopedGroup = services
                        .Where(s => s.Lifetime == ServiceLifetime.Scoped)
                        .Select(s => s.ServiceType.ToString());
                    var transientGroup = services
                        .Where(s => s.Lifetime == ServiceLifetime.Transient)
                        .Select(s => s.ServiceType.ToString());
                    PrintGroup("scoped", "0", "blue", scopedGroup);
                    PrintGroup("transient", "1", "lightgrey", transientGroup);
                    // Make interfaces different
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("     node [color=green]");
                    var interfacesGroup = services
                        .Where(s => s.ServiceType.IsInterface)
                        .Select(s => s.ServiceType.ToString());
                    foreach (var @interface in interfacesGroup)
                    {
                        sb.AppendLine($"     \"{@interface}\"");
                    }
                    sb.AppendLine();
                    sb.AppendLine("    node [color=black]");
                    var noninterfacesGroup = services
                        .Where(s => !s.ServiceType.IsInterface)
                        .Select(s => s.ServiceType.ToString());
                    foreach (var nonInterface in noninterfacesGroup)
                    {
                        sb.AppendLine($"     \"{nonInterface}\"");
                    }
                    //Now print dependencies
                    foreach (var d in dependencies)
                    {
                        sb.AppendLine($"     \"{d.Key}\" -> \"{d.Value}\"");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine("</pre>");
                    // Ok ready. Now return all the graph
                    await context.Response.WriteAsync(sb.ToString());
                })
        );

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
            if (OperatingSystem.IsWindows())
            {
                var cp = Process.GetCurrentProcess();
                cp.PriorityClass = ProcessPriorityClass.RealTime;
            }

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
