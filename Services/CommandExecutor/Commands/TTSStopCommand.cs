using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TtsStopCommand(IVoicer syntheziaVoicer) : BaseCommand
{
    public override string CommandName => "ttsstop";
    public override string Description => "Останавливает все текущие TTS-фразы";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await syntheziaVoicer.Stop();
        return "Остановил все ттс фразы!";
    }
}
