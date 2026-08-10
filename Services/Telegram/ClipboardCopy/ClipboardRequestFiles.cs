namespace MARS.Server.Services.Telegram.ClipboardCopy;

internal sealed class ClipboardRequestFiles(string[] memoryFileNames, DateTime createdAt)
{
    public string[] MemoryFileNames { get; } = memoryFileNames;
    public DateTime CreatedAt { get; } = createdAt;
}
