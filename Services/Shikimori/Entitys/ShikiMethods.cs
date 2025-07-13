namespace MARS.Server.Services.Shikimori.Entitys;

public static class ShikiMethods
{
    public static Waifu CreateWaifu(this ShikiCharacter character)
    {
        var shortestAnime = "";
        var shortestManga = "";

        shortestAnime = character.Animes.Exists(e => !string.IsNullOrWhiteSpace(e.Russian))
            ? character.Animes.Min(anime => anime.Russian)
            : null;

        if (character.Mangas.Exists(e => !string.IsNullOrWhiteSpace(e.Russian)))
        {
            var minLength = character.Mangas.Min(e => e.Russian.Length);
            shortestManga = character
                .Mangas.FirstOrDefault(x => x.Russian.Length == minLength)
                ?.Russian;
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
            Name = character.Russian,
            Anime = shortestAnime,
            Manga = shortestManga,
            WhenAdded = moscowTime,
            ShikiId = character.Id.ToString() ?? throw new NullReferenceException(),
            ImageUrl = character.Image.Original,
            LastOrder = moscowTime,
            IsPrivated = false,
            OrderCount = 0,
        };

        return waifu;
    }
}
