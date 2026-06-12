using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class CatisaCommand(ITwitchClient client) : BaseCommand
{
    public override string CommandName => "catisa";
    public override string Description => "Отправить сообщение на твич канал";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Api, Platform.Telegram];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "channel",
                Description = "Канал для отправки сообщения",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "text",
                Description = "Текст для отправки сообщения",
                Type = "string",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var message = (string)parameters["text"];
        var channel = (string)parameters["channel"];

        await client.JoinChannelAsync(channel);
        await client.SendMessageAsync(channel, message);

        return $"Отправил \"{message}\" на канал \"{channel}\"!";
    }
}
