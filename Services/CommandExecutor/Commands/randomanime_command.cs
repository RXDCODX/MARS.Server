using MARS.Server.Configuration;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Shikimori;
using Microsoft.Extensions.Options;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class RandomAnimeCommand(
    ShikimoriService shikimoriService,
    IOptions<ShikimoriClientOptions> shikimoriOptions
) : BaseCommand
{
    public override string CommandName => "randomanime";
    public override string Description => "Показать случайное аниме с Shikimori";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch, Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось получить случайное аниме, попробуйте позже.";

        var anime = await shikimoriService.GetRandomAnime();

        if (anime != null)
        {
            var site = shikimoriOptions.Value.ShikimoriSite.TrimEnd('/');
            var title = anime.Russian ?? anime.Name ?? "?";
            var year = anime.AiredOnYear?.ToString() ?? "?";

            result = $"{title} ({year} г.) — {site}/animes/{anime.Id}";
        }

        return result;
    }
}