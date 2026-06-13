using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RandomMemCommand(RandomMemOnline randomMemOnline) : BaseCommand
{
    public override string CommandName => "randommem";
    public override string Description => "Включает или выключает онлайн-режим рандомных мемов";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var usage = randomMemOnline.IsStop
            ? "Включил рандом мем онлайн!"
            : "Выключил рандом мем онлайн!";

        randomMemOnline.IsStop = !randomMemOnline.IsStop;

        return Task.FromResult(usage);
    }
}
