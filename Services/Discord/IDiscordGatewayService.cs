using DSharpPlus;
using DSharpPlus.EventArgs;

namespace MARS.Server.Services.Discord;

public interface IDiscordGatewayService
{
    bool IsConnected { get; }

    DiscordClient? Client { get; }

    void RegisterMessageCreatedHandler(Func<DiscordClient, MessageCreatedEventArgs, Task> handler);

    void RegisterVoiceStateUpdatedHandler(
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task> handler
    );

    Task<OperationResult> SendMessageAsync(
        ulong channelId,
        string message,
        CancellationToken cancellationToken = default
    );
}
