using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.Rewards;

public class RickRollerService(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IConfiguration configuration,
    TwitchUserEnsureService userEnsureService
)
{
    private readonly Random _rnd = new();

    private readonly MediaInfo _baseMediaDto = new()
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

    public double RickRollChance =>
        double.Parse(configuration["AppSettings:RickRoll:Chance"] ?? "0.05");

    public async Task<bool> TryRickRollAsync(TwitchUser user, Func<Task> whenNotRickRolled)
    {
        var roll = _rnd.NextDouble();
        if (roll < RickRollChance)
        {
            user = await userEnsureService.EnsureUserExistsAsync(user);

            var newDto = _baseMediaDto.CloneTo();
            newDto.TextInfo.KeyWordsColor = user.ChatColor;

            await hubContext.Clients.All.Alert(new MediaDto(newDto));
            //await client.SendMessageToMainTwitchAsync(
            //    $"@{user.UserLogin}, прости, но награда тебя рикрольнула! Ничего личного, просто рандом!"
            //);
            return true;
        }
        else
        {
            await whenNotRickRolled();
            return false;
        }
    }
}
