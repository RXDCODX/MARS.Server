using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Rewards.TwitchRandomBadAppleDay;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class BadAppleCommand(RandomBadAppleDay randomBadAppleDay) : BaseCommand
{
    public override string CommandName => "badapple";
    public override string Description => "Досрочная активация Bad Apple эффекта";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override string[] Aliases => ["ba", "randomba"];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        try
        {
            result = await randomBadAppleDay.ManualActivateAsync();
        }
        catch (Exception ex)
        {
            result = $"❌ Ошибка при активации Bad Apple: {ex.Message}";
        }

        return result;
    }
}
