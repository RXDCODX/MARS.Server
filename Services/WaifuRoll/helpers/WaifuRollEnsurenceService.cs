using MARS.Server.Services.Shikimori;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.WaifuRoll.helpers;

public class WaifuRollEnsurenceService(
    ILogger<WaifuRollEnsurenceService> logger,
    ShikimoriService shikiService,
    IDbContextFactory<AppDbContext> appDbContextFactory
) : ITelegramusService
{
    private readonly ILogger _logger = logger;

    public async Task<Waifu> EnsureWaifuHaveImageIrl(Waifu waifu)
    {
        if (string.IsNullOrWhiteSpace(waifu.ImageUrl))
        {
            var character = await shikiService.GetShikiCharacterById(long.Parse(waifu.ShikiId)); // FullCharacter
            if (character != null)
            {
                await using var dbContext = await appDbContextFactory.CreateDbContextAsync();
                waifu.ImageUrl = character.Image.Original;
                await dbContext
                    .Waifus.Where(e => e.ShikiId == waifu.ShikiId)
                    .ExecuteUpdateAsync(e =>
                        e.SetProperty(t => t.ImageUrl, character.Image.Original)
                    );
            }
        }

        return waifu;
    }

    public async Task<Waifu> EnsureMangaAndAnimeTitleExists(Waifu waifu)
    {
        var result = waifu;

        try
        {
            if (
                string.IsNullOrWhiteSpace(waifu.Manga)
                && string.IsNullOrWhiteSpace(waifu.Anime)
                && long.TryParse(waifu.ShikiId, out var characterId)
            )
            {
                await using var dbContext = await appDbContextFactory.CreateDbContextAsync();
                if (string.IsNullOrWhiteSpace(result.Anime))
                {
                    var animeTitle = await shikiService.GetCharacterAnimeTitle(characterId);
                    if (!string.IsNullOrWhiteSpace(animeTitle))
                    {
                        result.Anime = animeTitle;
                        await dbContext
                            .Waifus.Where(e => e.ShikiId == result.ShikiId)
                            .ExecuteUpdateAsync(e => e.SetProperty(t => t.Anime, animeTitle));
                    }
                }

                if (string.IsNullOrWhiteSpace(result.Manga))
                {
                    var mangaTitle = await shikiService.GetCharacterMangaTitle(characterId);
                    if (!string.IsNullOrWhiteSpace(mangaTitle))
                    {
                        result.Manga = mangaTitle;
                        await dbContext
                            .Waifus.Where(e => e.ShikiId == result.ShikiId)
                            .ExecuteUpdateAsync(e => e.SetProperty(t => t.Manga, mangaTitle));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при заполнении полей аниме и манги для вайфу {WaifuId}",
                waifu.ShikiId
            );
        }

        return result;
    }
}
