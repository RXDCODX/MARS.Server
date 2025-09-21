using MARS.Server.Services.Shikimori;

namespace MARS.Server.Services.WaifuRoll.helpers;

public class WaifuRollEnsurenceService(
    ILogger<WaifuRollEnsurenceService> logger,
    ShikimoriService shikiService
) : ITelegramusService
{
    private readonly ILogger _logger = logger;

    public async Task<Waifu> EnsureWaifuHaveImageIrl(Waifu waifu)
    {
        var result = waifu;

        if (!string.IsNullOrWhiteSpace(waifu.ImageUrl))
        {
            return result;
        }

        var character = await shikiService.GetShikiCharacterById(long.Parse(waifu.ShikiId)); // FullCharacter
        if (character != null)
        {
            result.ImageUrl = character.Image.Original;

            // Заполняем поля аниме и манги, если они пустые
            if (string.IsNullOrWhiteSpace(result.Anime) || string.IsNullOrWhiteSpace(result.Manga))
            {
                if (string.IsNullOrWhiteSpace(result.Anime))
                {
                    var animeTitle = await shikiService.GetCharacterAnimeTitle(character.Id);
                    if (!string.IsNullOrWhiteSpace(animeTitle))
                    {
                        result.Anime = animeTitle;
                    }
                }

                if (string.IsNullOrWhiteSpace(result.Manga))
                {
                    var mangaTitle = await shikiService.GetCharacterMangaTitle(character.Id);
                    if (!string.IsNullOrWhiteSpace(mangaTitle))
                    {
                        result.Manga = mangaTitle;
                    }
                }
            }
        }

        return result;
    }

    public async Task<Waifu> EnsureMangaAndAnimeTitleExists(Waifu waifu)
    {
        var result = waifu;

        try
        {
            if (long.TryParse(waifu.ShikiId, out var characterId))
            {
                if (string.IsNullOrWhiteSpace(result.Anime))
                {
                    var animeTitle = await shikiService.GetCharacterAnimeTitle(characterId);
                    if (!string.IsNullOrWhiteSpace(animeTitle))
                    {
                        result.Anime = animeTitle;
                    }
                }

                if (string.IsNullOrWhiteSpace(result.Manga))
                {
                    var mangaTitle = await shikiService.GetCharacterMangaTitle(characterId);
                    if (!string.IsNullOrWhiteSpace(mangaTitle))
                    {
                        result.Manga = mangaTitle;
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
