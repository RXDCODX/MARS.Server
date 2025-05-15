using System.Text;
using System.Text.RegularExpressions;

namespace MARS.Server.Exstensions;

public static class StringExtension
{
    public static ValueTask<MediaType> GetFileMediaTypeAsync(this string? exst)
    {
        return ValueTask.FromResult(GetFileMediaType(exst));
    }

    public static MediaType GetFileMediaType(this string? exst)
    {
        var exstension = exst?.ToLower();

        return exstension switch
        {
            ".tgs" => MediaType.TelegramSticker,
            ".ogg" => MediaType.Audio,
            ".oga" => MediaType.Audio,
            ".webm" => MediaType.Video,
            ".mp4" => MediaType.Video,
            ".jpg" => MediaType.Image,
            ".jpeg" => MediaType.Image,
            ".png" => MediaType.Image,
            ".webp" => MediaType.Image,
            ".gif" => MediaType.Gif,
            ".mp3" => MediaType.Audio,
            ".wav" => MediaType.Audio,
            _ => MediaType.None,
        };
    }

    public static string ReplaceTooLongWords(
        this string input,
        string replacement = "Слишком большое слово"
    )
    {
        const string pattern = @"\b\w{20,}\b";

        var result = Regex.Replace(input, pattern, replacement);

        return result;
    }

    public static string ReplaceLinks(this string input, string replacement = " ссылка ")
    {
        const string pattern = @"\bhttps?://\S+\b";

        var result = Regex.Replace(input, pattern, replacement);

        return result;
    }

    public static string CutTooLongText(
        this string input,
        ushort maxLength = 140,
        bool hardCut = false
    )
    {
        if (hardCut)
        {
            if (input.Length > 140)
            {
                return input[..140];
            }

            return input;
        }

        var splits = input.Split(' ');

        var count = 0;

        for (var index = 0; index < splits.Length; index++)
        {
            var split = splits[index];
            if (count + split.Length > maxLength)
            {
                return string.Join(' ', splits[..index]);
            }

            count += split.Length;
        }

        return input;
    }

    public static string[] SplitWithQuotes(this string text)
    {
        text = Regex.Replace(text.Trim(), @"\s+", " ");
        var list = new List<string>();
        var sb = new StringBuilder();
        var isQuoted = false;

        foreach (var c in text)
        {
            if (c == ' ' && !isQuoted)
            {
                if (sb.Length > 0)
                {
                    var word = sb.ToString();
                    sb.Clear();
                    list.Add(word);
                }

                continue;
            }

            if (c == '"')
            {
                if (isQuoted)
                {
                    isQuoted = false;
                    var word = sb.ToString();
                    sb.Clear();
                    list.Add(word);
                    continue;
                }
                else
                {
                    isQuoted = true;
                    continue;
                }
            }

            sb.Append(c);
        }

        if (isQuoted)
        {
            throw new Exception("ты насрал в ковычках");
        }

        return list.ToArray();
    }
}
