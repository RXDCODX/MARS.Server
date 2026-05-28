using System.Threading;
using MARS.Server.Services.Twitch.Rewards._5_AddWife;
using MARS.Server.Services.WaifuRoll;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;

public class AutoHello : BackgroundService
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

    public async Task AutoHelloTwitchEvent(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
