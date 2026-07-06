using System;
using System.Threading.Tasks;
using MARS.Server.Exstensions;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace MARS.Server.Services.Twitch.Rewards;

public class RickRollerService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IConfiguration configuration,
    TwitchUserEnsureService userEnsureService
)
{
    // ----- Dependencies -----------------------------------------------------

    private readonly Random _rnd = new();

    public double RickRollChance
    {
        get
        {
            // Retrieve the configured chance value. Use invariant culture to ensure the
            // decimal separator is interpreted correctly regardless of the system locale
            // (the original implementation relied on the current culture, causing parsing
            // failures on machines with a comma decimal separator, e.g., Russian locale).
            var confValueRaw = configuration["AppSettings:RickRoll:Chance"];
            var confValue = confValueRaw?.Trim();
            if (!string.IsNullOrWhiteSpace(confValue))
            {
                // Attempt to parse using invariant culture (dot as decimal separator).
                // This covers the expected configuration format.
                if (
                    double.TryParse(
                        confValue,
                        System.Globalization.NumberStyles.Float
                            | System.Globalization.NumberStyles.AllowThousands,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var value
                    )
                )
                {
                    return value;
                }
                // As a fallback, replace a possible comma with a dot and try again.
                var replaced = confValue.Replace(',', '.');
                if (
                    double.TryParse(
                        replaced,
                        System.Globalization.NumberStyles.Float
                            | System.Globalization.NumberStyles.AllowThousands,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value
                    )
                )
                {
                    return value;
                }
            }
            // Default chance when the configuration is missing or cannot be parsed.
            return 0.03;
        }
    }

    public async Task<bool> TryRickRollAsync(TwitchUser user, Func<Task> whenNotRickRolled)
    {
        var roll = _rnd.NextDouble();
        if (roll < RickRollChance)
        {
            user = await userEnsureService.EnsureUserExistsAsync(user);

            MediaInfo newDto = new()
            {
                FileInfo = new MediaFileInfo()
                {
                    Extension = ".mp4",
                    FileName = "rickroll.mp4",
                    FilePath = "Alerts\\rickroll.mp4",
                    Type = MediaType.Video,
                    IsLocalFile = true,
                },
                MetaInfo = new MediaMetaInfo()
                {
                    DisplayName = string.Empty,
                    Duration = 7,
                    IsLooped = false,
                    Priority = MediaAlertPriority.Normal,
                },
                PositionInfo = new MediaPositionInfo()
                {
                    IsRotated = true,
                    IsUseOriginalWidthAndHeight = true,
                    RandomCoordinates = true,
                },
                StylesInfo = new MediaStylesInfo(),
                TextInfo = new MediaTextInfo()
                {
                    Text = "#{user.name}# был рикрольнут на баллы канала!",
                    KeyWordSybmolDelimiter = '#',
                },
                Id = Guid.NewGuid(),
            };

            newDto.FixAlertText(user.DisplayName, string.Empty);
            newDto.TextInfo.KeyWordsColor = user.ChatColor;

            await hubContext.Clients.All.Alert(new MediaDto(newDto));
            return true;
        }
        else
        {
            await whenNotRickRolled();
            return false;
        }
    }
}
