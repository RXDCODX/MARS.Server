using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.KeyboardHook_UNUSED;

public static class KeyboardHookFactory
{
    public static IKeyboardHookService CreateKeyboardHookService(
        ILogger<IKeyboardHookService> logger,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        IHostApplicationLifetime lifetime
    )
    {
        return OperatingSystem.IsWindows()
            ? new WindowsKeyboardHookService(logger, hubContext, lifetime)
            : new NullKeyboardHookService(logger);
    }
}
