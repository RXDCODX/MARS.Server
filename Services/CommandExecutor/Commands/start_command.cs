namespace MARS.Server.Services.CommandExecutor.Commands;

public class StartCommand : BaseCommand
{
    public override string CommandName => "start";
    public override string Description => "Стартовая команда";
    public override bool IsAdminCommand => false;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            $$"""
            Привет! Это бот для интерактивного развлечения на твич канале https://twitch.tv/{{TwitchExstension.Channel}}.

            /commands или /c для списка доступных комманд.
            /help {команда} для информации о команде. 
            Пример пользования - /info
            """
        );
    }
}
