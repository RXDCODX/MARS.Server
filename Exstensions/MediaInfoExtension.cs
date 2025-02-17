namespace MARS.Server.Exstensions;

public static class MediaInfoExtension
{
    public static MediaInfo FixAlertText(this MediaInfo media, string username, string usertext)
    {
        if (media.TextInfo.Text?.ToLower().Contains("{user.text}") ?? false)
        {
            media.TextInfo.Text = usertext.StartsWith("@")
                ? media.TextInfo.Text.Replace("{user.text}", usertext.Substring(1).Trim())
                : media.TextInfo.Text.Replace("{user.text}", usertext.Trim());
        }

        if (
            media.TextInfo.Text?.Contains("{user.name}", StringComparison.OrdinalIgnoreCase)
            ?? false
        )
        {
            media.TextInfo.Text = media.TextInfo.Text.Replace("{user.name}", username);
        }

        return media;
    }
}
