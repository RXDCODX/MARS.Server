using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class AutomessageSendAutoMessageCommand(AutoMessagesHandler handler) : BaseCommand
{
    public override string CommandName => "automessage";
    public override string Description => "Отправить AutoMessage принудительно";
    public override bool IsAdminCommand => true;
    public override Platform[] AvailablePlatforms =>
        [Platform.Api, Platform.Telegram, Platform.Twitch];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        await handler.ExecuteAutoMessage();

        return "Автомессага выполнена!";
    }
}
