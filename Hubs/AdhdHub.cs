using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.Adhd;
using MARS.Server.Services.Adhd.Entities;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/adhd", AutoDiscover.MethodsAndParams)]
public class AdhdHub(AdhdLayoutService adhdLayoutService, ILogger<AdhdHub> logger) : Hub<IAdhdHub>
{
    public async Task GetCurrentConfig()
    {
        var config = await adhdLayoutService.GetCurrentConfigAsync();
        await Clients.Caller.ReceiveConfig(config);
    }

    public async Task UpdateConfig(AdhdLayoutConfigDto config)
    {
        var updated = await adhdLayoutService.UpdateConfigAsync(config);
        await Clients.Others.ConfigUpdated(updated);
        logger.LogInformation("ADHD layout config updated by {ConnectionId}", Context.ConnectionId);
    }

    [SwaggerIgnore]
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ReceiveConfig(await adhdLayoutService.GetCurrentConfigAsync());
    }

    [SwaggerIgnore]
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(
            "Client disconnected from adhd hub: {ConnectionId}",
            Context.ConnectionId
        );
        await base.OnDisconnectedAsync(exception);
    }
}
