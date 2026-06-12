using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TanyaTanyaCommand : BaseCommand
{
    public override string CommandName => "tanya";
    public override string Description => "Показывает ближайший рабочий день Тани";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms => [Platform.Telegram, Platform.Api];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var lastWorkDay1 = new DateTimeOffset(2024, 1, 22, 0, 0, 0, TimeSpan.Zero);

        var today = DateTimeOffset.Now;

        var nearestWorkDay = await FindNearestWorkDay(lastWorkDay1, today);

        return nearestWorkDay[0] == today
            ? $"Таня работает сегодня и {nearestWorkDay[1]:dd MMMM yyyy}"
            : $"Таня не работает сегодня. Ближайший рабочий день: {nearestWorkDay[0]:dd MMMM yyyy}";
    }

    private static ValueTask<DateTimeOffset[]> FindNearestWorkDay(
        DateTimeOffset lastWorkDay,
        DateTimeOffset today
    )
    {
        var timeSinceLastWorkDay = today - lastWorkDay;

        var daysSinceLastWorkDay = timeSinceLastWorkDay.Days;

        const int cycleLength = 4; // 2 дня работает, 2 дня отдыхает
        var day = daysSinceLastWorkDay % cycleLength;

        switch (day)
        {
            case 0:
                return new ValueTask<DateTimeOffset[]>([today, today.AddDays(1)]);
            case 1:
                return new ValueTask<DateTimeOffset[]>([today, today.AddDays(3)]);
            case 2:
                return new ValueTask<DateTimeOffset[]>(
                    [today.AddDays(cycleLength - day), today.AddDays(cycleLength - day + 1)]
                );
            case 3:
                goto case 2;
            default:
                throw new InvalidOperationException();
        }
    }
}
