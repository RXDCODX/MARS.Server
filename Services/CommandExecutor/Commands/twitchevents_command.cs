namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitcheventsCommand(TokenService tokenService, EventSubService eventSubService)
    : BaseCommand
{
    public override string CommandName => "twitchevents";
    public override string Description => "Показывает список активных подписок Twitch EventSub";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (tokenService.Token is null)
        {
            return "Не удалось провести запрос";
        }

        var response = await eventSubService.GetEventSubsAsync();

        if (response == null)
        {
            return "Не удалось провести запрос";
        }

        var subs = response.Subscriptions.Select(e => $"{e.Type} - {e.Status}");

        return $"Подключенные сабы твича:{Environment.NewLine} {string.Join(Environment.NewLine, subs)}";
    }
}
