using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class CatisaCommand(ITwitchClient client) : BaseCommand
{
    public override string CommandName => "catisa";
    public override string Description => "Отправить сообщение на твич канал";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Api, Platform.Telegram];

    public override CommandParameterInfo[] Parameters => [
        new CommandParameterInfo { Name = "channel", Description = "Канал для отправки сообщения", Type = "string", Required = true },
        new CommandParameterInfo { Name = "message", Description = "Текст для отправки сообщения", Type = "string", Required = true }
    ];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var message = (string)parameters["message"];
        var channel = (string)parameters["channel"];

        Task.Factory.StartNew(
            () =>
            {
                client.SendMessage(channel, message);
            },
            cancellationToken
        );

        return Task.FromResult($"Отправил \"{message}\" на канал \"{channel}\"!");
    }
}
