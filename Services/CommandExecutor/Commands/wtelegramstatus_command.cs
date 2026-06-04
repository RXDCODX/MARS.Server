using MARS.Server.Services.Telegram.WTelegram;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class WtelegramstatusCommand(WTelegramClientService clientService) : BaseCommand
{
    public override string CommandName => "wtelegramstatus";
    public override string Description =>
        "Проверка, способен ли WTelegramClient принимать сообщения";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var status = await clientService.GetClientStatusAsync(cancellationToken);

        var sb = new StringBuilder();

        sb.AppendLine(status.IsAuthenticated ? "✅ Авторизован" : "⚠️ Не авторизован");

        if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
        {
            sb.AppendLine($"Ошибка: {status.ErrorMessage}");
        }

        if (status.UserId is not null)
        {
            sb.AppendLine($"UserId: {status.UserId}");
        }

        if (!string.IsNullOrWhiteSpace(status.Username))
        {
            sb.AppendLine($"Username: {status.Username}");
        }

        if (!string.IsNullOrWhiteSpace(status.Phone))
        {
            sb.AppendLine($"Phone: {status.Phone}");
        }

        if (sb.Length == 0)
        {
            sb.Append("Нет данных о статусе");
        }

        return sb.ToString();
    }
}
