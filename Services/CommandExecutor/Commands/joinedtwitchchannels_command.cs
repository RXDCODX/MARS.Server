using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class JoinedTwitchChannelsCommand(ITwitchClient client) : BaseCommand
{
    public override string CommandName => "joinedtwitchchannels";
    public override string Description =>
        "Показывает список Twitch-каналов, к которым подключён бот";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var channels = client.JoinedChannels;

        return channels.Count == 0
            ? Task.FromResult("Бот не подключен ни к одному Twitch-каналу")
            : Task.FromResult(string.Join(Environment.NewLine, channels.Select(e => e.Channel)));
    }
}
