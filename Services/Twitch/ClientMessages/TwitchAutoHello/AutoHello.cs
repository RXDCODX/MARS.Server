using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;

public class AutoHello
{
    private readonly ILogger<AddNewWaifu> _logger;
    private readonly ITwitchClient _client;
    private readonly WaifuRollService _waifuRollService;

    public AutoHello(
        ILogger<AddNewWaifu> logger,
        ITwitchClient client,
        WaifuRollService waifuRollService,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _logger = logger;
        _client = client;
        _waifuRollService = waifuRollService;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += AutoHelloTwitchEvent;
        });
    }

    public async void AutoHelloTwitchEvent(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await Task.Run(async () =>
            {
                var message = await _waifuRollService.AutoHello(
                    args.ChatMessage.UserId,
                    args.ChatMessage.Username
                );

                if (!string.IsNullOrWhiteSpace(message))
                {
                    await _client.SendMessageToMainTwitchAsync(message, _logger);
                }
            });
        }
    }
}
