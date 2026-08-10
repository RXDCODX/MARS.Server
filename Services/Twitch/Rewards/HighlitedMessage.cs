using System.Reflection;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.AutoArts_OBSOLETE.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using MARS.Server.Services.Twitch.Validation;
using Microsoft.AspNetCore.SignalR;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.Rewards;

public class HighlitedMessage(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment environment,
    IPuntoSwitcherService puntoSwitcherService,
    ITwitchClient client,
    IHostApplicationLifetime applicationLifetime,
    RickRollerService rickRollerService,
    ITwitchEventValidationService validator
) : BackgroundService
{
    internal async Task TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        var vr = await validator
            .ForMessageReceived(args)
            .RequireChannel()
            .SkipBlacklisted()
            .RequireFollower()
            .ValidateWithResponseAsync(args.ChatMessage.Username);

        if (vr.IsInvalid)
        {
            return;
        }

        if (
            (
                args.ChatMessage.UserDetail.IsVip
                || args.ChatMessage.UserDetail.IsModerator
                || args.ChatMessage.IsBroadcaster
            ) && args.ChatMessage.IsHighlighted
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
                        if (puntoSwitcherService.IsFilterEnabled)
                        {
                            var fixedMessage = puntoSwitcherService.TryFixMessage(
                                args.ChatMessage.Message
                            );
                            if (fixedMessage is { Success: true, Data.HasChanges: true })
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
