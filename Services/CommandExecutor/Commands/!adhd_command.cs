using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class AdhdCommand(IHubContext<TelegramusHub, ITelegramusHub> alertsHub) : BaseCommand
{
    public override string CommandName => "adhd";
    public override string Description =>
        "Активирует ADHD эффект на указанное количество секунд или переключает перманентный режим (без параметра)";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override string[] Aliases => ["adhdactivate", "adhdstart"];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "seconds",
                Description =
                    "Количество секунд для активации ADHD эффекта (если не указано — перманентный режим)",
                Type = "int",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось активировать ADHD эффект";

        if (parameters.TryGetValue("seconds", out var secondsObj))
        {
            if (int.TryParse(secondsObj.ToString(), out var seconds) && seconds > 0)
            {
                try
                {
                    // Активируем ADHD эффект через SignalR хаб на указанное время
                    await alertsHub.Clients.All.Adhd(seconds);

                    result = $"✅ ADHD эффект активирован на {seconds} секунд!";
                }
                catch (Exception ex)
                {
                    result = $"❌ Ошибка при активации ADHD эффекта: {ex.Message}";
                }
            }
            else
            {
                result = "Количество секунд должно быть положительным числом";
            }
        }
        else
        {
            try
            {
                // Без параметра — перманентное переключение оверлея
                await alertsHub.Clients.All.Adhd(null);

                result = "✅ ADHD эффект переключён в перманентный режим!";
            }
            catch (Exception ex)
            {
                result = $"❌ Ошибка при переключении ADHD эффекта: {ex.Message}";
            }
        }

        return result;
    }
}
