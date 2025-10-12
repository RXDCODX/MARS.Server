using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class TanyaCommand : BaseCommand
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
        // Дата последнего рабочего дня Тани
        var lastWorkDay1 = new DateTimeOffset(2024, 1, 22, 0, 0, 0, TimeSpan.Zero);

        // Текущая дата
        var today = DateTimeOffset.Now;

        // Найти ближайший рабочий день Тани
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
        // Время, прошедшее с последнего рабочего дня
        var timeSinceLastWorkDay = today - lastWorkDay;

        // Сдвиг в днях от последнего рабочего дня
        var daysSinceLastWorkDay = timeSinceLastWorkDay.Days;

        // Найти ближайший рабочий день
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
