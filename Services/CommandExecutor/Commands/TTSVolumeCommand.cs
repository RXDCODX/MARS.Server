using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.CommandExecutor.Commands;


public class TtsVolumeCommand(IVoicer syntheziaVoicer) : BaseCommand
{
    public override string CommandName => "ttsvolume";
    public override string Description => "Устанавливает или показывает текущую громкость TTS";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api, Platform.Discord, Platform.Vk, Platform.Twitch];
    public override CommandParameterInfo[] Parameters => [
        new CommandParameterInfo { Name = "volume", Description = "Громкость (0-100, 0 блокирует TTS)", Type = "int", Required = false }
    ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (parameters.TryGetValue("volume", out var volumeObj))
        {
            var volume = Convert.ToInt32(volumeObj);

            if (OperatingSystem.IsWindows())
            {
                if (volume < 1)
                {
                    await syntheziaVoicer.Block();
                }
                else
                {
                    await syntheziaVoicer.Unblock();
                }

                syntheziaVoicer.ChangeVolume(volume);
                return $"Громкость была установленнна на {volume}";
            }
            else
            {
                return "Громкость не может быть изминена на Linux";
            }
        }
        else
        {
            return "Текущая громкость - " + syntheziaVoicer.GetVolume();
        }
    }
}

