using System;
using System.Threading.Tasks;
using MARS.Server.CustomLoggers.SignalRLogger;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Hubs.Filters;

public class LoggerHubRecursionFilter(LoggerHubRecursionGuard recursionGuard) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        object? result;

        if (IsLoggerHub(invocationContext.Hub))
        {
            var suppressionScope = recursionGuard.BeginSuppression();

            try
            {
                result = await next(invocationContext);
            }
            finally
            {
                suppressionScope.Dispose();
            }
        }
        else
        {
            result = await next(invocationContext);
        }

        return result;
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next
    )
    {
        if (IsLoggerHub(context.Hub))
        {
            recursionGuard.TrackLoggerHubConnection(context.Context.ConnectionId);
            var suppressionScope = recursionGuard.BeginSuppression();

            try
            {
                await next(context);
            }
            finally
            {
                suppressionScope.Dispose();
            }
        }
        else
        {
            await next(context);
        }
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next
    )
    {
        if (IsLoggerHub(context.Hub))
        {
            var suppressionScope = recursionGuard.BeginSuppression();

            try
            {
                await next(context, exception);
            }
            finally
            {
                recursionGuard.UntrackLoggerHubConnection(context.Context.ConnectionId);
                suppressionScope.Dispose();
            }
        }
        else
        {
            await next(context, exception);
        }
    }

    private static bool IsLoggerHub(Hub hub)
    {
        var result = hub is LoggerHub;
        return result;
    }
}
