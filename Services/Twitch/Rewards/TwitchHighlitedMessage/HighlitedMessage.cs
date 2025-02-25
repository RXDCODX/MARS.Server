using Hangfire;
using MARS.Server.Services.AutoArts.Entitys;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchHighlitedMessage;

public class HighlitedMessage
{
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly IWebHostEnvironment _environment;

    public HighlitedMessage(
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IWebHostEnvironment environment,
        ITwitchClient client,
        IHostApplicationLifetime lifetime
    )
    {
        _hubContext = hubContext;
        _environment = environment;

        lifetime.ApplicationStarted.Register(
            () => client.OnMessageReceived += TwitchClientOnNormalMessage
        );
    }

    internal void TwitchClientOnNormalMessage(object? sender, OnMessageReceivedArgs args)
    {
        BackgroundJob.Enqueue(() => Process(args));
    }

    public async Task Process(OnMessageReceivedArgs args)
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
        )
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

            await _hubContext.Clients.All.Highlite(args.ChatMessage, color, image);
        }
    }

    private Image GetImageByFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new NullReferenceException();
        }

        var exstension = Path.GetExtension(filePath);
        filePath = filePath.Substring(
            filePath.IndexOf("wwwroot", StringComparison.Ordinal) + "wwwroot".Length
        );

        return new Image() { URL = filePath, Extension = exstension };
    }
}
