using Hangfire;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [Admin]
    public Task<Message> OnStopAppCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        BackgroundJob.Enqueue(() => Shutdown());

        var answer = botClient.SendMessage(
            message.Chat,
            "Shutdown!",
            cancellationToken: cancellationToken
        );

        return answer;
    }

    [Ignore]
    public async Task Shutdown()
    {
        await Task.Delay(10);
        applicationLifetime.StopApplication();
    }
}
