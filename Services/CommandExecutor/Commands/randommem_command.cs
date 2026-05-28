using System.Collections.Generic;
using System.Threading;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RandomMemCommand() : BaseCommand
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
        var usage = RandomMemOnline.IsStop
            ? "Включил рандом мем онлайн!"
            : "Выключил рандом мем онлайн!";

        RandomMemOnline.IsStop = !RandomMemOnline.IsStop;

        return Task.FromResult(usage);
    }
}
