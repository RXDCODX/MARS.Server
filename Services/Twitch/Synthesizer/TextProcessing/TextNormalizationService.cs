using System.Text;

namespace MARS.Server.Services.Twitch.Synthesizer.TextProcessing;

/// <summary>
/// Service for normalizing text for TTS synthesis.
/// Handles removal or replacement of non-Cyrillic characters.
/// </summary>
public interface ITextNormalizationService
{
    /// <summary>
    /// Normalizes text by removing or replacing non-Cyrillic characters.
    /// </summary>
    /// <param name="text">Text to normalize</param>
    /// <param name="replaceMode">If true, replaces non-Cyrillic with alternatives; if false, removes them</param>
    /// <returns>Normalized text</returns>
    string Normalize(string text, bool replaceMode = false);

    /// <summary>
    /// Checks if text contains any non-Cyrillic characters.
    /// </summary>
    /// <param name="text">Text to check</param>
    /// <returns>True if contains non-Cyrillic characters</returns>
    bool HasNonCyrillicCharacters(string text);
}

public class TextNormalizationService : ITextNormalizationService
{
    // Mapping table for transliterating non-Cyrillic characters to Cyrillic equivalents
    private static readonly Dictionary<string, string> CyrillicReplacements = new()
    {
        // Latin to Cyrillic transliteration
        { "A", "А" },
        { "a", "а" },
        { "B", "Б" },
        { "b", "б" },
        { "C", "С" },
        { "c", "с" },
        { "D", "Д" },
        { "d", "д" },
        { "E", "Е" },
        { "e", "е" },
        { "F", "Ф" },
        { "f", "ф" },
        { "G", "Г" },
        { "g", "г" },
        { "H", "Х" },
        { "h", "х" },
        { "I", "И" },
        { "i", "и" },
        { "J", "Ж" },
        { "j", "ж" },
        { "K", "К" },
        { "k", "к" },
        { "L", "Л" },
        { "l", "л" },
        { "M", "М" },
        { "m", "м" },
        { "N", "Н" },
        { "n", "н" },
        { "O", "О" },
        { "o", "о" },
        { "P", "П" },
        { "p", "п" },
        { "R", "Р" },
        { "r", "р" },
        { "S", "С" },
        { "s", "с" },
        { "T", "Т" },
        { "t", "т" },
        { "U", "У" },
        { "u", "у" },
        { "V", "В" },
        { "v", "в" },
        { "W", "В" },
        { "w", "в" },
        { "X", "Х" },
        { "x", "х" },
        { "Y", "Ы" },
        { "y", "ы" },
        { "Z", "З" },
        { "z", "з" },
        // Special characters
        { " ", " " },
        { ",", "," },
        { ".", "." },
        { "!", "!" },
        { "?", "?" },
        { ";", ";" },
        { ":", ":" },
        { "-", "-" },
        { "'", "'" },
        { "\"", "\"" },
        { "(", "(" },
        { ")", ")" },
        { "[", "[" },
        { "]", "]" },
        { "{", "{" },
        { "}", "}" },
    };

    public string Normalize(string text, bool replaceMode = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (replaceMode)
        {
            return ReplaceNonCyrillic(text);
        }

        return RemoveNonCyrillic(text);
    }

    public bool HasNonCyrillicCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var character in text)
        {
            if (
                !IsCyrillicCharacter(character)
                && !char.IsWhiteSpace(character)
                && !char.IsPunctuation(character)
            )
            {
                return true;
            }
        }

        return false;
    }

    private string RemoveNonCyrillic(string text)
    {
        var result = new StringBuilder();

        foreach (var character in text)
        {
            if (
                IsCyrillicCharacter(character)
                || char.IsWhiteSpace(character)
                || char.IsPunctuation(character)
            )
            {
                result.Append(character);
            }
        }

        return result.ToString().Trim();
    }

    private string ReplaceNonCyrillic(string text)
    {
        var result = new StringBuilder();

        foreach (var character in text)
        {
            var charStr = character.ToString();

            if (
                IsCyrillicCharacter(character)
                || char.IsWhiteSpace(character)
                || char.IsPunctuation(character)
            )
            {
                result.Append(character);
            }
            else if (CyrillicReplacements.TryGetValue(charStr, out var replacement))
            {
                result.Append(replacement);
            }
            // else: skip unknown characters
        }

        return result.ToString().Trim();
    }

    private static bool IsCyrillicCharacter(char c)
    {
        // Unicode Cyrillic block: U+0400 to U+04FF
        return c >= '\u0400' && c <= '\u04FF';
    }
}
