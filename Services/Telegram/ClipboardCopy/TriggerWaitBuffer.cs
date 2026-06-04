namespace MARS.Server.Services.Telegram.ClipboardCopy;

internal sealed class TriggerWaitBuffer
{
    public bool HasTrigger { get; set; }
    public CancellationTokenSource? TimeoutCts { get; set; }
}
