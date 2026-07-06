using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Rewards._5_AddWife;
using MARS.Server.Services.Twitch.Validation;
using MARS.Server.Services.WaifuRoll;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;

public class AutoHello : BackgroundService
{
    private readonly ILogger<AddNewWaifu> _logger;
    private readonly ITwitchClient _client;
    private readonly WaifuRollService _waifuRollService;
    private readonly ITwitchEventValidationService _validator;

    public AutoHello(
        ILogger<AddNewWaifu> logger,
        ITwitchClient client,
        WaifuRollService waifuRollService,
        IHostApplicationLifetime applicationLifetime,
        ITwitchEventValidationService validator
    )
    {
        _logger = logger;
        _client = client;
        _waifuRollService = waifuRollService;
        _validator = validator;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += AutoHelloTwitchEvent;
        });
    }

    public async Task AutoHelloTwitchEvent(object? sender, OnMessageReceivedArgs args)
    {
        var result = await _validator
            .ForMessageReceived(args)
            .RequireChannel()
            .SkipBlacklisted()
            .ValidateWithResponseAsync(args.ChatMessage.Username);

        if (result.IsInvalid)
        {
            return;
        }

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
