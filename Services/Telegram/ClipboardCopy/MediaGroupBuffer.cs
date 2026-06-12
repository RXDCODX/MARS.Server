using System.Collections.Concurrent;
using System.Threading;

namespace MARS.Server.Services.Telegram.ClipboardCopy;

internal sealed class MediaGroupBuffer
{
    public ConcurrentDictionary<int, Message> Messages { get; } = new();
    public CancellationTokenSource? DebounceCts { get; set; }
    public int IsProcessed;

    public void ResetDebounce()
    {
        DebounceCts?.Cancel();
        DebounceCts?.Dispose();
    }
}
