using Telegram.Bot.Types.ReplyMarkups;

namespace MARS.Server.Services.TelegramBotService.Commands;

public partial class Commands
{
    [AdminAtribbute]
    public async Task<Message> OnTanyaCommandReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        // Дата последнего рабочего дня Тани
        var lastWorkDay1 = new DateTimeOffset(2024, 1, 22, default, default, default, default);
        var lastWorkDay2 = new DateTimeOffset(2024, 1, 23, default, default, default, default);

        // Текущая дата
        var today = DateTimeOffset.Now;

        // Найти ближайший рабочий день Тани
        var nearestWorkDay = await FindNearestWorkDay(lastWorkDay1, today);

        if (nearestWorkDay[0] == today)
        {
            return await botClient.SendMessage(
                message.Chat.Id,
                $"Таня работает сегодня и {nearestWorkDay[1]:dd MMMM yyyy}",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken
            );
        }

        return await botClient.SendMessage(
            message.Chat.Id,
            $"Таня не работает сегодня. Ближайший рабочий день: {nearestWorkDay[0]:dd MMMM yyyy}",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken
        );
    }

    private ValueTask<DateTimeOffset[]> FindNearestWorkDay(
        DateTimeOffset lastWorkDay,
        DateTimeOffset today
    )
    {
        // Время, прошедшее с последнего рабочего дня
        var timeSinceLastWorkDay = today - lastWorkDay;

        // Сдвиг в днях от последнего рабочего дня
        var daysSinceLastWorkDay = timeSinceLastWorkDay.Days;

        // Найти ближайший рабочий день
        var cycleLength = 4; // 2 дня работает, 2 дня отдыхает
        var day = daysSinceLastWorkDay % cycleLength;

        switch (day)
        {
            case 0:
                return new ValueTask<DateTimeOffset[]>(new[] { today, today.AddDays(1) });
            case 1:
                return new ValueTask<DateTimeOffset[]>(new[] { today, today.AddDays(3) });
            case 2:
                return new ValueTask<DateTimeOffset[]>(
                    new[] { today.AddDays(cycleLength - day), today.AddDays(cycleLength - day + 1) }
                );
            case 3:
                goto case 2;
            default:
                throw new InvalidOperationException();
        }
    }
}
