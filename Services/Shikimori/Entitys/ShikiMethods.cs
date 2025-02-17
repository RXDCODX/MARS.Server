namespace MARS.Server.Services.Shikimori.Entitys;

public static class ShikiMethods
{
    public static Waifu CreateWaifu(this ShikiCharacter character)
    {
        var shortestAnime = "";
        var shortestManga = "";

        shortestAnime = character.animes.Exists(e => e.russian != null)
            ? character.animes.Min(Anime => Anime.russian)
            : null;

        if (character.mangas.Exists(e => e.russian != null))
        {
            var minLength = character.mangas.Min(e => e.russian.Length);
            shortestManga = character
                .mangas.FirstOrDefault(x => x.russian.Length == minLength)
                ?.russian;
        }
        else
        {
            shortestManga = null;
        }

        var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time")
        );

        var waifu = new Waifu
        {
            Name = character.russian,
            Anime = shortestAnime,
            Manga = shortestManga,
            WhenAdded = moscowTime,
            ShikiId = character.id.ToString() ?? throw new NullReferenceException(),
            ImageUrl = character.image.original,
            LastOrder = moscowTime,
            IsPrivated = false,
            OrderCount = 0,
        };

        return waifu;
    }
}
