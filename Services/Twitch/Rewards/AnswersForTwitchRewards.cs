using System.Collections.Generic;
using System.Linq;
using ShikimoriSharp.Classes;

namespace MARS.Server.Services.Twitch.Rewards;

public class AnswersForTwitchRewards
{
    public static readonly Dictionary<Command, string> Answers = new()
    {
        { Command.AddNewWaifu, "@{user}, новый супруг {waifuName} добавлен(-а)!" },
        { Command.GetRandomAnime, "@{user}, твое рандомное аниме - {animeTitle}" },
        { Command.GetRandomManga, "@{user}, твоя рандомная манга - {mangaTitle}" },
        { Command.MergeWaifu, "Произошла свадьба между @{user} и {waifuName}! Совет да любовь!" },
        { Command.RollWaifu, "@{user}, тебе выпал(-а) {waifuName} из {waifuTitle}!" },
    };

    private static readonly IEnumerable<string> Keywords =
    [
        "{user}",
        "{waifuName}",
        "{animeTitle}",
        "{mangaTitle}",
        "{waifuTitle}",
    ];

    public static string ReplaceKeywordsInAnswer(
        string displayName,
        string message,
        AnimeID? shikiAnime = null,
        MangaID? shikiMangas = null,
        Waifu? waifu = null
    )
    {
        if (Keywords.Any(e => message.Contains(e)))
        {
            var keywords = Keywords.Where(e => message.Contains(e));

            foreach (var keyword in keywords)
            {
                switch (keyword)
                {
                    case "{user}":
                        message = message.Replace(keyword, displayName);
                        break;
                    case "{waifuName}":
                        message = message.Replace(
                            keyword,
                            waifu!.Name ?? throw new NullReferenceException("waifu был null # ")
                        );
                        break;
                    case "{animeTitle}":
                        message = message.Replace(
                            keyword,
                            shikiAnime?.Russian
                                ?? throw new NullReferenceException("shikiAnime был null")
                        );
                        break;
                    case "{mangaTitle}":
                        message = message.Replace(
                            keyword,
                            shikiMangas?.Russian
                                ?? throw new NullReferenceException("shikiMangas был null")
                        );
                        break;
                    case "{waifuTitle}":
                        var title = string.IsNullOrWhiteSpace(waifu!.Anime)
                            ? waifu.Manga
                                ?? throw new NullReferenceException(
                                    "waifu.Anime и waifu.Manga был null"
                                )
                            : waifu.Anime;
                        message = message.Replace(keyword, title);
                        break;
                }
            }
        }

        return message;
    }
}
