using System.Text.RegularExpressions;

namespace MARS.Server.Exstensions;

public static class StringExtension
{
    public static ValueTask<MediaType> GetFileMediaTypeAsync(this string? exst)
    {
        var exstension = exst?.ToLower();

        return exstension switch
        {
            ".tgs" => ValueTask.FromResult(MediaType.TelegramSticker),
            ".ogg" => ValueTask.FromResult(MediaType.Audio),
            ".oga" => ValueTask.FromResult(MediaType.Audio),
            ".webm" => ValueTask.FromResult(MediaType.Video),
            ".mp4" => ValueTask.FromResult(MediaType.Video),
            ".jpg" => ValueTask.FromResult(MediaType.Image),
            ".jpeg" => ValueTask.FromResult(MediaType.Image),
            ".png" => ValueTask.FromResult(MediaType.Image),
            ".webp" => ValueTask.FromResult(MediaType.Image),
            ".gif" => ValueTask.FromResult(MediaType.Gif),
            ".mp3" => ValueTask.FromResult(MediaType.Audio),
            ".wav" => ValueTask.FromResult(MediaType.Audio),
            _ => ValueTask.FromResult(MediaType.None),
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
}
