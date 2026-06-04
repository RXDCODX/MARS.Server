namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrClearCommand(SoundRequestCommandsService soundRequestCommandsService) : BaseCommand
{
    public override string CommandName => "srclear";
    public override string Description => "Очистить очередь звуковых запросов";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override CommandVisibility Visibility => CommandVisibility.All;

    public override CommandParameterInfo[] Parameters => [];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        TwitchUser? user = null;

        if (parameters.TryGetValue("user", out var userObj))
        {
            if (userObj is TwitchUser twitchUser)
            {
                user = twitchUser;
            }
        }

        if (user == null)
        {
            result = "Не удалось получить информацию о пользователе";
        }
        else
        {
            var hasPermission = true;
            if (platform == Platform.Twitch)
            {
                hasPermission = user.IsBroadcaster;
            }

            if (!hasPermission)
            {
                result = "Очистить очередь могут только Broadcaster";
            }
            else
            {
                result = await soundRequestCommandsService.ClearQueueAsync(cancellationToken);
            }
        }

        return result;
    }
}
