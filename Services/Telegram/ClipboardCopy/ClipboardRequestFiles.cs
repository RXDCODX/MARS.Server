namespace MARS.Server.Services.Telegram.ClipboardCopy;

internal sealed class ClipboardRequestFiles(string[] memoryFileNames, DateTimeOffset createdAt)
{
    public string[] MemoryFileNames { get; } = memoryFileNames;
    public DateTimeOffset CreatedAt { get; } = createdAt;
}
