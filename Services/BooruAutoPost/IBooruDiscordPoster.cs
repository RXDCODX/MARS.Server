namespace MARS.Server.Services.BooruAutoPost;

public interface IBooruDiscordPoster
{
    Task<OperationResult> PostAsync(
        ulong channelId,
        byte[] fileBytes,
        string fileName,
        string? message,
        CancellationToken cancellationToken
    );
}
