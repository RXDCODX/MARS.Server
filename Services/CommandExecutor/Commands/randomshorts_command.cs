using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.YouTube;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class randomshorts_command(YouTubeResolver resolver) : BaseCommand
{
    public override string CommandName => "randomshorts";
    public override string Description => "Получить случайный Short с ютуба";
    public override bool IsAdminCommand => true;

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                DefaultValue = "Shorts dank meme",
                Description = "Query для ютуба",
                Name = "query",
                Required = false,
                Type = "string",
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var video = await resolver.ResolveQueryAsync("Shorts dank meme", cancellationToken);

        return video != null ? video.Url.AbsoluteUri : "Чёт не нашёл не одного видео";
    }
}
