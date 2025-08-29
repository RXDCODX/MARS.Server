using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class AdhdCommand(IHubContext<TelegramusHub, ITelegramusHub> alertsHub) : BaseCommand
{
    public override string CommandName => "adhd";
    public override string Description => "Активирует ADHD эффект на указанное количество секунд";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override string[] Aliases => ["adhdactivate", "adhdstart"];

    public override CommandParameterInfo[] Parameters =>
        [
            new CommandParameterInfo
            {
                Name = "seconds",
                Description = "Количество секунд для активации ADHD эффекта",
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
        if (!parameters.TryGetValue("seconds", out var secondsObj))
        {
            return "Необходимо указать количество секунд";
        }

        if (!int.TryParse(secondsObj.ToString(), out var seconds) || seconds <= 0)
        {
            return "Количество секунд должно быть положительным числом";
        }

        try
        {
            // Активируем ADHD эффект через SignalR хаб
            await alertsHub.Clients.All.Adhd(seconds);

            return $"✅ ADHD эффект активирован на {seconds} секунд!";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка при активации ADHD эффекта: {ex.Message}";
        }
    }
}
