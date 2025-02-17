using System.Text.RegularExpressions;

namespace MARS.Server.Exstensions;

public static class StringExtension
{
    public static ValueTask<MediaType> GetFileMediaTypeAsync(this string? exst)
    {
        var exstension = exst?.ToLower();

        switch (exstension)
        {
            case ".tgs":
                return ValueTask.FromResult(MediaType.TelegramSticker);
            case ".ogg":
                return ValueTask.FromResult(MediaType.Audio);
            case ".oga":
                return ValueTask.FromResult(MediaType.Audio);
            case ".webm":
                return ValueTask.FromResult(MediaType.Video);
            case ".mp4":
                return ValueTask.FromResult(MediaType.Video);
            case ".jpg":
                return ValueTask.FromResult(MediaType.Image);
            case ".jpeg":
                return ValueTask.FromResult(MediaType.Image);
            case ".png":
                return ValueTask.FromResult(MediaType.Image);
            case ".webp":
                return ValueTask.FromResult(MediaType.Image);
            case ".gif":
                return ValueTask.FromResult(MediaType.Gif);
            case ".mp3":
                return ValueTask.FromResult(MediaType.Audio);
            case ".wav":
                return ValueTask.FromResult(MediaType.Audio);
            default:
                return ValueTask.FromResult(MediaType.None);
        }
    }

    public static string ReplaceTooLongWords(this string input)
    {
        // ���������� ���������� ��������� ��� ������ ���� ������ 20 � ����� ��������
        string pattern = @"\b\w{20,}\b";
        string replacement = "������� ������� �����";

        // �������� ��� ��������� ����� �� "������� ������� �����"
        string result = Regex.Replace(input, pattern, replacement);

        return result;
    }

    public static string ReplaceLinks(this string input)
    {
        // ���������� ��������� ��� ������ ������, ������������ � http:// ��� https://
        var pattern = @"\bhttps?://\S+\b";

        // ������ ���� ������ �� ����� "������"
        var result = Regex.Replace(input, pattern, " ������ ");

        return result;
    }
}
