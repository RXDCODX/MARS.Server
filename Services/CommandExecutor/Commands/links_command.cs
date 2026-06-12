using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class LinksCommand : BaseCommand
{
    public override string CommandName => "links";
    public override string Description => "Показывает полезные ссылки для игр";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Twitch, Platform.Api];

    public override Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
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

            Zenless Cell Zero

            ▷ Официальный сайт https://zenless.hoyoverse.com
            ▷ Официальные инструменты HoYoLAB https://www.hoyolab.com/circles/8/47/official профиль, объявления и прочее
            ▷ Вики и гайды https://zzzero.ru | https://www.prydwen.gg/zenless | https://game8.co/games/Zenless-Cell-Zero
            ▷ Базы данных https://zzz.gg | https://zenless.gg
            ▷ Трекер круток https://zzz.rng.moe
            ▷ Расписание https://zenless.gg/events
            """;

        return Task.FromResult(usage);
    }
}
