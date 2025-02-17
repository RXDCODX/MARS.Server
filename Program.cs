using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MARS.Server.CustomLoggers.TelegramLogger;
using MARS.Server.Services.Honkai;
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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
            directory = Environment.GetEnvironmentVariable("ZYZ_SERVICE_PATH");
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new NullReferenceException();
            }

            Environment.CurrentDirectory = directory;
        }

        /////////////////////////////////////////////////////////////////////////////////////////

        services.AddSingleton<IDbContextFactory<AppDbContext>>(contextFactory);

        if (builder.Environment.IsDevelopment())
        {
            var contextOptionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            contextOptionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
            contextOptionsBuilder.EnableThreadSafetyChecks();
            if (builder.Environment.IsDevelopment())
            {
                contextOptionsBuilder.EnableDetailedErrors();
                contextOptionsBuilder.EnableSensitiveDataLogging();

                contextOptionsBuilder.UseNpgsql(configuration.GetConnectionString("Dev_Path"));
            }
            else
            {
                contextOptionsBuilder.UseNpgsql(configuration.GetConnectionString("Prod_Path"));
            }

            services.AddSingleton(contextOptionsBuilder.Options);
        }

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

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = Int64.MaxValue; // Лимит для multipart/form-data запросов
            options.ValueLengthLimit = Int32.MaxValue; // Лимит для отдельных значений формы
            options.ValueCountLimit = Int32.MaxValue;
            ; // Лимит на количество значений формы
        });

        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = Int64.MaxValue; // Лимит для всего тела запроса
        });

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

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        if (isWithSpa)
        {
            app.MapFallbackToFile("index.html");
        }

        var cp = Process.GetCurrentProcess();
        cp.PriorityClass = ProcessPriorityClass.RealTime;

        app.Run();
    }
}
