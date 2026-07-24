using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace MARS.Server.Services.Discord.Gateway;

public interface IDiscordGatewayService
{
    bool IsConnected { get; }

    DiscordClient? Client { get; }

    void RegisterMessageCreatedHandler(Func<DiscordClient, MessageCreatedEventArgs, Task> handler);

    void RegisterVoiceStateUpdatedHandler(
        Func<DiscordClient, VoiceStateUpdatedEventArgs, Task> handler
    );

    void RegisterInteractionCreatedHandler(
        Func<DiscordClient, InteractionCreatedEventArgs, Task> handler
    );

    void RegisterComponentInteractionCreatedHandler(
        Func<DiscordClient, ComponentInteractionCreatedEventArgs, Task> handler
    );

    Task<DiscordClient?> EnsureConnectedAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> SendMessageAsync(
        ulong channelId,
        string message,
        CancellationToken cancellationToken = default
    );

    Task<OperationResult> SendFileAsync(
        ulong channelId,
        Stream fileStream,
        string fileName,
        string? message = null,
        CancellationToken cancellationToken = default
    );
}
