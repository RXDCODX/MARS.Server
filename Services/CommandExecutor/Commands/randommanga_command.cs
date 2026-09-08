using MARS.Server.Configuration;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Shikimori;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RandomMangaCommand(
    ShikimoriService shikimoriService,
    IOptions<ShikimoriClientOptions> shikimoriOptions
) : BaseCommand
{
    public override string CommandName => "randommanga";
    public override string Description => "Показать случайную мангу с Shikimori";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось получить случайную мангу, попробуйте позже.";

        var manga = await shikimoriService.GetRandomManga();

        if (manga != null)
        {
            var site = shikimoriOptions.Value.ShikimoriSite.TrimEnd('/');
            var title = manga.Russian ?? manga.Name ?? "?";
            var year = manga.AiredOnYear?.ToString() ?? "?";

            result = $"{title} ({year} г.) — {site}/mangas/{manga.Id}";
        }

        return result;
    }
}