using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.SoundRequest;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class SrVolumeCommand(StateManager stateManager) : BaseCommand
{
    public override string CommandName => "srvolume";
    public override string Description => "Установить громкость звуковых запросов";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "volume",
                Description = "Громкость в процентах от 0 до 100",
                Type = "int",
                Required = true,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось установить громкость";

        if (parameters.TryGetValue("volume", out var volumeObj))
        {
            var volumeText = volumeObj?.ToString();
            if (int.TryParse(volumeText, out var volume))
            {
                if (volume >= 0 && volume <= 100)
                {
                    await stateManager.SetVolumeAsync(volume, notify: true);
                    result = $"Громкость звуковых запросов установлена на {volume}%";
                }
                else
                {
                    result = "Громкость должна быть от 0 до 100";
                }
            }
            else
            {
                result = "Необходимо указать число от 0 до 100";
            }
        }
        else
        {
            result = "Необходимо указать громкость от 0 до 100";
        }

        return result;
    }
}