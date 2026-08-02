using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TwitchchannelreconnectCommand(TwitchConnectionManager manager, ITwitchClient client)
    : BaseCommand
{
    public override string CommandName => "twitchchannelreconnect";
    public override string Description => "Выполняет реконнект к Twitch-чату";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var ok = await manager.ReconnectAsync();
        if (!ok)
        {
            return "Не удалось выполнить реконнект";
        }

        var marker = Guid.NewGuid().ToString("N")[..8];
        var verificationMessage = $"reconnect_check:{marker}";
        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        Task Handler(object? sender, OnMessageReceivedArgs args)
        {
            if (
                args.ChatMessage.Username.Equals(
                    TwitchExstension.BotName,
                    StringComparison.OrdinalIgnoreCase
                )
                && args.ChatMessage.Message == verificationMessage
            )
            {
                tcs.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        try
        {
            client.OnMessageReceived += Handler;
            await client.SendMessageToMainTwitchAsync(verificationMessage);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token,
                cancellationToken
            );

            var received = await tcs.Task.WaitAsync(linkedCts.Token);
            return received
                ? "Реконнект выполнен, соединение проверено"
                : "Реконнект выполнен, но проверка не подтверждена";
        }
        catch (OperationCanceledException)
        {
            return "Реконнект выполнен, но таймаут проверки (5 сек)";
        }
        finally
        {
            client.OnMessageReceived -= Handler;
        }
    }
}
