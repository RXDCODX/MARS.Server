using System.Reflection;
using MARS.Server.Services.AutoArts_OBSOLETE.Entitys;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.Rewards.TwitchHighlitedMessage;

public class HighlitedMessage : BackgroundService
{
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IPuntoSwitcherService _puntoSwitcherService;

    public HighlitedMessage(
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IWebHostEnvironment environment,
        IPuntoSwitcherService puntoSwitcherService,
        ITwitchClient client,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _hubContext = hubContext;
        _environment = environment;
        _puntoSwitcherService = puntoSwitcherService;

        applicationLifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += TwitchClientOnNormalMessage;
        });
    }

    internal async void TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        if (
            (
                args.ChatMessage.IsVip
                || args.ChatMessage.IsModerator
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
                {
                    var color = string.IsNullOrWhiteSpace(args.ChatMessage.ColorHex)
                        ? "#ffffff"
                        : args.ChatMessage.ColorHex;
                    var path = Path.Combine(_environment.WebRootPath, "faces");
                    var image = GetImageByFilePath(
                        Directory
                            .GetFiles(path, "*", SearchOption.AllDirectories)
                            .OrderBy(e => Random.Shared.Next())
                            .First()
                    );

                    var message = args.ChatMessage;
                    if (PuntoSwitcherState.IsFilterEnabled)
                    {
                        var fixedMessage = _puntoSwitcherService.TryFixMessage(
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

                    await _hubContext.Clients.All.Highlite(message, color, image);
                }
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
        return Task.CompletedTask;
    }
}
