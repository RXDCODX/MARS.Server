using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using TwitchLib.Client.Events;
using TwitchUserModel = MARS.Server.Services.Twitch.Entitys.TwitchUser;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class SyntheziaQueueManager(
    IVoicer voicer,
    ITwitchClient client,
    ILogger<SyntheziaQueueManager> logger
) : BackgroundService
{
    public bool IsServiceActive { get; set; } = true;

    /// <summary>
    /// Мгновенно останавливает озвучку и блокирует возможность озвучивать новые сообщения
    /// </summary>
    public async Task StopAndBlockAsync()
    {
        await voicer.Stop();
        logger.LogInformation("Озвучка остановлена и заблокирована.");
    }

    public async Task HandMessageToVoice(object? sender, OnMessageReceivedArgs args)
    {
        if (
            args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(e =>
                e.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
            && IsServiceActive
            && voicer.IsActive
        )
        {
            await Task.Run(async () =>
            {
                var currentMessage = args.ChatMessage.Message;

                var twitchUser = TwitchUserModel.FromChatMessage(args.ChatMessage);
                var message = currentMessage
                    .Trim()
                    .CutTooLongText()
                    .ReplaceLinks()
                    .ReplaceTooLongWords();

                if (twitchUser is not null)
                {
                    // Forward immediately to voicer which now broadcasts to AudioController
                    await voicer.Sound(twitchUser, message);
                }
            });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += HandMessageToVoice;
        }

        // wait until shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= HandMessageToVoice;
        await base.StopAsync(cancellationToken);
    }
}
