using System.Reflection;
using MARS.Server.Services.Twitch;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands(
    IWebHostEnvironment environment,
    IDbContextFactory<AppDbContext> factory,
    ITwitchClient client,
    UserAuthHelper userAuthHelper,
    IVoicer syntheziaVoicer,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    HelloVideoWorker helloVideoWorker
) : ITelegramusService
{
    public const string Template =
        "Не получилось получить комманды бота, сообщите об этой ошибке разработчику";

    internal async Task<Message> OnUsageCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken,
        bool isAdminCall = false
    )
    {
        Type commands = typeof(Commands);
        MethodInfo[] methods;

        if (isAdminCall)
        {
            methods = commands.GetMethods(
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public
            );
        }
        else
        {
            methods = commands
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.GetCustomAttribute<AdminAtribbute>() == null)
                .ToArray();
        }

        string usage;

        if (methods.Any())
        {
            var names = GetCommandName(methods);
            usage = string.Join(Environment.NewLine, names);
        }
        else
        {
            usage = Template;
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }

    private string[] GetCommandName(MethodInfo[] methods)
    {
        var commandNames = new string[methods.Length];
        const string template = "OnCommandReceived";

        for (var i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            var length = method.Name.Length - template.Length;
            var name = method.Name.Substring(2, length);
            commandNames[i] = "/" + name.ToLower();
        }

        return commandNames;
    }
}
