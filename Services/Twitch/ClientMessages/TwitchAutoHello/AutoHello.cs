using Hangfire;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello;

public class AutoHello(
    ILogger<AddNewWaifu> logger,
    ITwitchClient client,
    WaifuRollService waifuRollService
)
{
    public async void AutoHelloTwitchEvent(object? sender, OnMessageReceivedArgs args)
    {
        await Task.Run(async () =>
        {
            if (
                args.ChatMessage.Channel.Equals(
                    TwitchExstension.Channel,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var message = await waifuRollService.AutoHello(
                    args.ChatMessage.UserId,
                    args.ChatMessage.Username
                );

                if (!string.IsNullOrWhiteSpace(message))
                {
                    BackgroundJob.Enqueue(
                        () => client.SendMessageToPyrokxnezxzAsync(message, logger)
                    );
                }
            }
        });
    }
}
