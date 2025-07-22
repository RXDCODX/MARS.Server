using System.Reflection;
using MARS.Server.Services.Framedata;
using MARS.Server.Services.Twitch.HelloVideos;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;
using MARS.Server.Services.Twitch.Synthesizer.Enitity;
using MARS.Server.Services.WaifuRoll;
using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands(
    IWebHostEnvironment environment,
    IDbContextFactory<AppDbContext> factory,
    ITwitchClient client,
    IVoicer syntheziaVoicer,
    IHubContext<TelegramusHub, ITelegramusHub> alertsHub,
    HelloVideoWorker helloVideoWorker,
    EventSubService eventSubService,
    TokenService tokenService,
    MergeWaifu mergeWaifu,
    WaifuRollService waifoRollService,
    Tekken8FrameData frameData
) : ITelegramusService
{
    public const string Template =
        "Не получилось получить комманды бота, сообщите об этой ошибке разработчику";

    [Description("Показывает список всех доступных команд бота")]
    public async Task<Message> OnCommandsCommandReceived(
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
            methods =
            [
                .. commands
                    .GetMethods(
                        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public
                    )
                    .Where(method => method.GetCustomAttribute<AdminAttribute>() == null),
            ];
        }

        string usage;

        if (methods.Length != 0)
        {
            var names = GetCommandNameWithDescription(methods);
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

    [Ignore]
    private static string[] GetCommandNameWithDescription(MethodInfo[] methods)
    {
        var commandNames = new string[methods.Length];
        const string template = "OnCommandReceived";

        for (var i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            var length = method.Name.Length - template.Length;
            var name = method.Name.Substring(2, length);
            var command = "/" + name.ToLower();
            var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                commandNames[i] = $"{command} - {description}";
            }
            else
            {
                commandNames[i] = command;
            }
        }

        return commandNames;
    }
}
