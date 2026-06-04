namespace MARS.Server.Services.CommandExecutor.Commands;

public class DiscordCommand : BaseCommand
{
    public override string CommandName => "discord";
    public override string Description => "Команда для Discord";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Discord];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "channel",
                Description = "Канал Discord",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "message",
                Description = "Сообщение",
                Type = "string",
                Required = false,
            },
        ];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var channel = parameters["channel"].ToString() ?? "general";
        var message = parameters.TryGetValue("message", out var msgObj)
            ? msgObj.ToString()
            : "Привет из Discord!";

        var result = $"""
            🤖 Discord команда выполнена!
            📺 Канал: #{channel}
            💬 Сообщение: {message}

            Эта команда доступна только в Discord.
            """;

        return Task.FromResult(result);
    }
}
