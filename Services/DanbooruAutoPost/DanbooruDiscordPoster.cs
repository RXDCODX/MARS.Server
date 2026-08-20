using MARS.Server.Services.Discord.Gateway;

namespace MARS.Server.Services.DanbooruAutoPost;

public class DanbooruDiscordPoster(IDiscordGatewayService discordGatewayService)
    : IDanbooruDiscordPoster
{
    public async Task<OperationResult> PostAsync(
        ulong channelId,
        byte[] fileBytes,
        string fileName,
        string? message,
        CancellationToken cancellationToken
    )
    {
        var result = OperationResult.Bad("Не удалось отправить в Discord");

        try
        {
            await using var stream = new MemoryStream(fileBytes);
            result = await discordGatewayService.SendFileAsync(
                channelId,
                stream,
                fileName,
                message,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            result = OperationResult.Bad($"Ошибка Discord: {ex.Message}");
        }

        return result;
    }
}
