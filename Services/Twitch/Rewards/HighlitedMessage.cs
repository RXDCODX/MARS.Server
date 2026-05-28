using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MARS.Server.Services.AutoArts_OBSOLETE.Entitys;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.Rewards;

public class HighlitedMessage(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment environment,
    IPuntoSwitcherService puntoSwitcherService,
    ITwitchClient client,
    IHostApplicationLifetime applicationLifetime,
    RickRollerService rickRollerService
) : BackgroundService
{
    internal async Task TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        if (
            (
                args.ChatMessage.UserDetail.IsVip
                || args.ChatMessage.UserDetail.IsModerator
                || args.ChatMessage.IsBroadcaster
            )
            && args.ChatMessage.IsHighlighted
            && args.ChatMessage.Channel.Equals(
                TwitchExstension.Channel,
                StringComparison.OrdinalIgnoreCase
            )
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(args.ChatMessage.Username, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                await rickRollerService.TryRickRollAsync(
                    TwitchUser.FromOnMessageReceivedArgs(args)!,
                    async () =>
                    {
                        var color = string.IsNullOrWhiteSpace(args.ChatMessage.HexColor)
                            ? "#ffffff"
                            : args.ChatMessage.HexColor;
                        var path = Path.Combine(environment.WebRootPath, "faces");
                        var image = GetImageByFilePath(
                            Directory
                                .GetFiles(path, "*", SearchOption.AllDirectories)
                                .OrderBy(e => Random.Shared.Next())
                                .First()
                        );

                        var message = args.ChatMessage;
                        if (PuntoSwitcherState.IsFilterEnabled)
                        {
                            var fixedMessage = puntoSwitcherService.TryFixMessage(
                                args.ChatMessage.Message
                            );
                            if (fixedMessage.Success && fixedMessage.Data.HasChanges)
                            {
                                message = TryOverrideMessage(
                                    message,
                                    fixedMessage.Data.CorrectedMessage
                                );
                            }
                        }

                        await hubContext.Clients.All.Highlite(message, color, image);
                    }
                );
            });
        }
    }

    private static ChatMessage TryOverrideMessage(ChatMessage source, string correctedMessage)
    {
        var result = source;

        if (!string.IsNullOrWhiteSpace(correctedMessage))
        {
            var backingField = source
                .GetType()
                .GetField(
                    "<Message>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

            backingField?.SetValue(source, correctedMessage);

            result = source;
        }

        return result;
    }

    private static AutoArtImage GetImageByFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new NullReferenceException();
        }

        var exstension = Path.GetExtension(filePath);
        filePath = filePath.Substring(
            filePath.IndexOf("wwwroot", StringComparison.Ordinal) + "wwwroot".Length
        );

        return new AutoArtImage { URL = filePath, Extension = exstension };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += TwitchClientOnNormalMessage;
        });

        return Task.CompletedTask;
    }
}
