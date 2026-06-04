using MARS.Server.Services.Twitch.Rewards._1580_MikuBeam;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MikubeamCommand(TwitchMikuBeamRewardService mikuBeamService) : BaseCommand
{
    public override string CommandName => "mikubeam";
    public override string Description => "Досрочная активация MIKU MIKU BEAM эффекта";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override string[] Aliases => ["mikumikubeam", "mmb"];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        string result;

        try
        {
            result = await mikuBeamService.ManualActivateAsync();
        }
        catch (Exception ex)
        {
            result = $"❌ Ошибка при активации MIKU MIKU BEAM: {ex.Message}";
        }

        return result;
    }
}
