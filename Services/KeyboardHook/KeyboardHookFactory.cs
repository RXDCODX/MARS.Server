namespace MARS.Server.Services.KeyboardHook;

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
