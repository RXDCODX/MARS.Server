using System.Text;
using System.Text.RegularExpressions;

namespace MARS.Server.Exstensions;

public static class StringExtension
{
    extension(string? exst)
    {
        public ValueTask<MediaType> GetFileMediaTypeAsync()
        {
            return ValueTask.FromResult(exst.GetFileMediaType());
        }

        public MediaType GetFileMediaType()
        {
            var exstension = exst?.ToLower();

            return exstension switch
            {
                ".tgs" => MediaType.TelegramSticker,
                ".ogg" or ".oga" => MediaType.Audio,
                ".webm" or ".mp4" => MediaType.Video,
                ".jpg" or ".jpeg" or ".png" or ".webp" => MediaType.Image,
                ".gif" => MediaType.Gif,
                ".mp3" or ".wav" => MediaType.Audio,
                _ => MediaType.None,
            };
        }
    }

    extension(string input)
    {
        public string ReplaceTooLongWords(string replacement = "Слишком большое слово")
        {
            const string pattern = @"\b\w{20,}\b";
            var result = Regex.Replace(input, pattern, replacement);
            return result;
        }

        public string ReplaceLinks(string replacement = " ссылка ")
        {
            const string pattern = @"\bhttps?://\S+\b";
            var result = Regex.Replace(input, pattern, replacement);
            return result;
        }

        public string CutTooLongText(ushort maxLength = 140, bool hardCut = false)
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

        public string[] SplitWithQuotes()
        {
            var text = Regex.Replace(input.Trim(), @"\s+", " ");
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
        { 'Е', "E" },
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

    extension(string text)
    {
        public string ToEnglishTransliteration()
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var result = new StringBuilder(text.Length * 2);

            for (var i = 0; i < text.Length; i++)
            {
                var upperChar = char.ToUpper(text[i]);

                if (TransliterationMap.TryGetValue(upperChar, out var englishLetter))
                {
                    if (upperChar is 'Е' or (char)1122)
                    {
                        englishLetter = ShouldUseYe(i, text) ? "Ye" : "E";
                    }

                    result.Append(englishLetter);
                }
                else
                {
                    result.Append(text[i]);
                }
            }

            return result.ToString();
        }
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
