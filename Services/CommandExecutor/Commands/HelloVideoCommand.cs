using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.HelloVideos;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class HelloVideoCommand(HelloVideoWorker helloVideoWorker) : BaseCommand
{
    public override string CommandName => "hellovideo";
    public override string Description =>
        "Отправляет приветственное видео пользователю или с указанным цветом";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];
    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "name",
                Description = "Имя пользователя",
                Type = "string",
                Required = true,
            },
            new()
            {
                Name = "color",
                Description = "Цвет (опционально)",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        if (!parameters.TryGetValue("name", out var nameObj))
        {
            return "Необходимо указать имя пользователя";
        }

        var name = nameObj.ToString() ?? "";
        name = name.StartsWith('@') ? name.Substring(1) : name;
        var color = parameters.TryGetValue("color", out var colorObj) ? colorObj?.ToString() : null;

        string? resultName;
        if (!string.IsNullOrWhiteSpace(color))
        {
            resultName = await helloVideoWorker.TestVideo(name, color);
            if (resultName != null)
            {
                return $"Отправл приветствующий видос на имя {resultName} с цветом {color}";
            }
        }
        else
        {
            resultName = await helloVideoWorker.TestVideo(name);
            if (resultName != null)
            {
                return $"Отправл приветствующий видос на имя {resultName}";
            }
        }

        return "Кривые параметры";
    }
}
