namespace MARS.Server.Helpers;

public class PacificStandardTimeMidnightEvent : BackgroundService
{
    // Событие, которое будет вызываться в полночь PST
    public event EventHandler MidnightPstReached = (sender, args) => { };

    private DateTimeOffset _lastMidnight;

    public PacificStandardTimeMidnightEvent()
    {
        // Находим часовой пояс PST
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var ddTime = DateTimeOffset.UtcNow;
        var newDateTime = TimeZoneInfo.ConvertTime(ddTime, timeZoneInfo);
        _lastMidnight = newDateTime;
    }

    public async Task StartMonitoring(CancellationToken stoppingToken)
    {
        // Бесконечный цикл для мониторинга времени
        await Task.Factory.StartNew(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Ждем до полуночи PST
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    // Проверяем, наступила ли полночь
                    if (DateTimeOffset.UtcNow.Date > _lastMidnight)
                    {
                        // Вызываем событие
                        MidnightPstReached?.Invoke(this, EventArgs.Empty);
                        // Обновляем дату для следующего цикла
                        _lastMidnight = _lastMidnight.AddDays(1);
                    }
                }
            },
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return StartMonitoring(stoppingToken);
    }
}
