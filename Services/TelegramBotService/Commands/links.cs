using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    public async Task<Message> OnLinksCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        const string usage = """
            Honkai Star Rail

            ▷ Базы данных ‒ https://hsr.honeyhunterworld.com/?lang=RU | https://hsr.yatta.top/ru/archive/avatar
            ▷ Гайды, таблицы ‒ https://www.prydwen.gg/star-rail
            ▷ Календарь событий ‒ https://pom.moe/timeline
            ▷ Оценка предметов ‒ https://www.mobilemeta.gg/honkai-starrail/app/relic-scorer
            ▷ Планер улучшения ‒ https://starrailstation.com/ru/planner | https://hsr.seelie.me/planner
            ▷ Промокоды ‒ https://honkai-star-rail.fandom.com/wiki/Redemption_Code
            ▷ Cтатистика, веб-ивенты ‒ https://www.hoyolab.com/circles/6/39/official
            ▷ Трекеры ‒ https://starrailstation.com/ru/warp#char_event | https://pom.moe/warp
            ▷ Калькулятор - https://zeeka32.github.io/Star-Rail-Damage-Calculator/

            Zenless Zone Zero

            ▷ Официальный сайт https://zenless.hoyoverse.com
            ▷ Официальные инструменты HoYoLAB https://www.hoyolab.com/circles/8/47/official профиль, объявления и прочее
            ▷ Вики и гайды https://zzzero.ru | https://www.prydwen.gg/zenless | https://game8.co/games/Zenless-Zone-Zero
            ▷ Базы данных https://zzz.gg | https://zenless.gg
            ▷ Трекер круток https://zzz.rng.moe
            ▷ Расписание https://zenless.gg/events
            """;

        return await botClient.SendMessage(
            message.Chat.Id,
            usage,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }
}
