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
            return input.Length > 140 ? input[..140] : input;
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

        return isQuoted ? throw new Exception("ты насрал в ковычках") : [.. list];
    }
}

public static class RussianToEnglishTransliteration
{
    private static readonly Dictionary<char, string?> TransliterationMap = new()
    {
        { 'А', "A" },
        { 'Б', "B" },
        { 'В', "V" },
        { 'Г', "G" },
        { 'Д', "D" },
        { 'Е', "E" }, // Default case, special handling below
        { 'Ё', "Yo" },
        { 'Ж', "Zh" },
        { 'З', "Z" },
        { 'И', "I" },
        { 'Й', "Y" },
        { 'К', "K" },
        { 'Л', "L" },
        { 'М', "M" },
        { 'Н', "N" },
        { 'О', "O" },
        { 'П', "P" },
        { 'Р', "R" },
        { 'С', "S" },
        { 'Т', "T" },
        { 'У', "U" },
        { 'Ф', "F" },
        { 'Х', "Kh" },
        { 'Ц', "Ts" },
        { 'Ч', "Ch" },
        { 'Ш', "Sh" },
        { 'Щ', "Shch" },
        { 'Ъ', "" },
        { 'Ы', "Y" },
        { 'Ь', "" },
        { 'Э', "E" },
        { 'Ю', "Yu" },
        { 'Я', "Ya" },
        { ' ', " " },
        //{ (char)769, "" }, // accent mark
    };

    private static readonly HashSet<char> Vowels =
    [
        'А',
        'Е',
        'Ё',
        'И',
        'І',
        'О',
        'У',
        'Ы',
        'Э',
        'Ю',
        'Я',
        'Ѣ',
        'Ѵ',
    ];

    public static string ToEnglishTransliteration(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length * 2); // Allocate extra space for potential multi-character transliterations

        for (var i = 0; i < text.Length; i++)
        {
            var upperChar = char.ToUpper(text[i]);

            if (TransliterationMap.TryGetValue(upperChar, out var englishLetter))
            {
                // Special handling for Е and Ѣ (Yat)
                if (upperChar is 'Е' or (char)1122)
                {
                    englishLetter = ShouldUseYe(i, text) ? "Ye" : "E";
                }

                result.Append(englishLetter);
            }
            else
            {
                // If character not in our map, keep it as-is
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    private static bool ShouldUseYe(int currentIndex, string text)
    {
        return IsAtStart(currentIndex, text)
            || IsVowel(text[currentIndex - 1])
            || text[currentIndex - 1] == 'Ъ'
            || text[currentIndex - 1] == 'Ь';
    }

    private static bool IsAtStart(int index, string text)
    {
        return index == 0 || text[index - 1] == ' ';
    }

    private static bool IsVowel(char c)
    {
        return Vowels.Contains(char.ToUpper(c));
    }
}
