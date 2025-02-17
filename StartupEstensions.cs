using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages;
using MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;
using MARS.Server.Services.Twitch.FumoFriday;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Rewards.MiniGames;
using MARS.Server.Services.Twitch.Rewards.TwitchAlerts;
using MARS.Server.Services.Twitch.Rewards.TwitchHighlitedMessage;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomMeme;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.StreamBotNotifications;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace MARS.Server;

public static class StartupEstensions
{
    internal static IServiceCollection AddTwitchEvents(
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
        twitchApi.Settings.AccessToken = twitchApi.Auth.GetAccessTokenAsync().Result;
        twitchApi.Settings.Secret = twitchConfig.ClientSecret;
        twitchApi.Settings.Scopes = [AuthScopes.Any];

        services.AddSingleton<ITwitchAPI>(twitchApi);

        var credentials = new ConnectionCredentials(
            TwitchExstension.BotName,
            "7ebls4048aw0atuopgj7zh36xyvufp"
        );

        var client = new TwitchClient(default, default, factory.CreateLogger<TwitchClient>());

        client.Initialize(credentials, TwitchExstension.Channel);
        client.Connect();
        services.AddSingleton<ITwitchClient>(client);

        services.AddSingleton<TwitchStreamStartupNotifications>();
        services.AddSingleton<TwitchMediaAlerts>();
        services.AddSingleton<AutoMessagesController>();
        services.AddSingleton<UserAuthHelper>();
        services.AddHostedService(sp => sp.GetRequiredService<UserAuthHelper>());
        services.AddSingleton<AutoHello>();
        services.AddSingleton<AddNewWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<AddNewWaifu>());
        services.AddSingleton<MergeWaifu>();
        services.AddHostedService(sp => sp.GetRequiredService<MergeWaifu>());
        services.AddSingleton<RollWaifu>();
        services.AddSingleton<RandomMeme>();
        services.AddSingleton<TwitchRussianRoulete>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchRussianRoulete>());
        services.AddSingleton<TwitchTrivia>();
        services.AddHostedService(sp => sp.GetRequiredService<TwitchTrivia>());
        services.AddSingleton<HighlitedMessage>();
        services.AddSingleton<FumoFridayWorker>();
        services.AddSingleton<HelloVideoWorker>();

        return services;
    }
}
