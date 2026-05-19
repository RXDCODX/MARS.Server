using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Exstensions;

public static class MediaInfoExtension
{
    extension(MediaInfo media)
    {
        public MediaInfo FixAlertText(string username, string usertext, string? usercolor = "white")
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

            if (media.TextInfo.Text?.Contains("{user.color}", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                media.TextInfo.Text = media.TextInfo.Text.Replace("{user.color}", usercolor);
            }

            return media;
        }

        public MediaInfo FixAlertText(TwitchUser user, string message)
        {
            return media.FixAlertText(user.DisplayName, message, user.ChatColor);
        }
    }
}
