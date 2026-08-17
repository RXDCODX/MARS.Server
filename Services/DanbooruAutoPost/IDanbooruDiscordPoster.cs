namespace MARS.Server.Services.DanbooruAutoPost;

public interface IDanbooruDiscordPoster
{
    Task<OperationResult> PostAsync(
        ulong channelId,
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken
    );
}
