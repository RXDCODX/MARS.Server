namespace MARS.Server.Exstensions;

public static class MediaInfoExtension
{
    extension(MediaInfo media)
    {
        public MediaInfo FixAlertText(string username, string usertext)
        {
            if (
                media
                    .TextInfo.Text?.ToLower()
                    .Contains("{user.text}", StringComparison.CurrentCultureIgnoreCase) ?? false
            )
            {
                media.TextInfo.Text = usertext.StartsWith('@')
                    ? media.TextInfo.Text.Replace("{user.text}", usertext[1..].Trim())
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

        public MediaInfo FixAlertText(TwitchUser user, string message)
        {
            return media.FixAlertText(user.DisplayName, message);
        }

        public MediaInfo FixAlertColor(TwitchUser user)
        {
            if (media.TextInfo.KeyWordsColor?.Contains("{user.color}") ?? false)
            {
                media.TextInfo.KeyWordsColor = media.TextInfo.KeyWordsColor.Replace("{user.color}", user.ChatColor);
            }

            if (media.TextInfo.TextColor?.Contains("{user.color}") ?? false)
            {
                media.TextInfo.TextColor = media.TextInfo.TextColor?.Replace("{user.color}", user.ChatColor);
            }

            return media;
        }
    }
}
