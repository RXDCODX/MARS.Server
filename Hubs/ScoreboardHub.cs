using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Scoreboard;
using MARS.Server.Services.Scoreboard.Entitys;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/scoreboard", AutoDiscover.MethodsAndParams)]
public class ScoreboardHub(ScoreboardService scoreboardService, ILogger<ScoreboardHub> logger)
    : Hub<IScoreboardHub>
{
    public async Task JoinAsClient()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "client");
        logger.LogInformation("Client joined scoreboard hub: {ConnectionId}", Context.ConnectionId);
    }

    public async Task GetCurrentState()
    {
        var state = await scoreboardService.GetCurrentStateAsync();
        await Clients.Caller.ReceiveState(state);
    }

    [SwaggerIgnore]
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ReceiveState(await scoreboardService.GetCurrentStateAsync());
    }

    public async Task UpdateState(ScoreboardDto state)
    {
        await scoreboardService.UpdateStateAsync(state);
        // Отправляем всем клиентам, кроме отправителя, чтобы избежать рекурсии
        await Clients.Others.StateUpdated(state);
        logger.LogInformation("Scoreboard state updated by {ConnectionId}", Context.ConnectionId);
    }

    public async Task UpdatePlayerScore(int playerPosition, int newScore)
    {
        var success = await scoreboardService.UpdatePlayerScoreAsync(playerPosition, newScore);
        if (success)
        {
            // Отправляем всем клиентам, кроме отправителя
            await Clients.Others.PlayerScoreUpdated(playerPosition, newScore);
            logger.LogInformation(
                "Player {Position} score updated to {Score} by {ConnectionId}",
                playerPosition,
                newScore,
                Context.ConnectionId
            );
        }
    }

    public async Task SetPlayerFinal(int playerPosition, string final)
    {
        var success = await scoreboardService.SetPlayerFinalAsync(playerPosition, final);
        if (success)
        {
            // Отправляем всем клиентам, кроме отправителя
            await Clients.Others.PlayerFinalUpdated(playerPosition, final);
            logger.LogInformation(
                "Player {Position} final status set to {Final} by {ConnectionId}",
                playerPosition,
                final,
                Context.ConnectionId
            );
        }
    }

    public async Task SetVisibility(bool isVisible)
    {
        var success = await scoreboardService.SetVisibilityAsync(isVisible);
        if (success)
        {
            // Отправляем всем клиентам, кроме отправителя
            await Clients.Others.VisibilityChanged(isVisible);
            logger.LogInformation(
                "Scoreboard visibility set to {IsVisible} by {ConnectionId}",
                isVisible,
                Context.ConnectionId
            );
        }
    }

    public async Task ForceProcessPendingUpdates()
    {
        await scoreboardService.ForceProcessPendingUpdates();
        logger.LogInformation(
            "Forced processing of pending updates requested by {ConnectionId}",
            Context.ConnectionId
        );
    }

    [SwaggerIgnore]
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(
            "Client disconnected from scoreboard hub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnDisconnectedAsync(exception);
    }
}
