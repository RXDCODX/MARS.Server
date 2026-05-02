using System.Globalization;
using MARS.Server.Services;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.WaifuRoll;

namespace MARS.Server.Services.Twitch.WeddingAnniversary;

public class WeddingAnniversaryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    TwitchUserEnsureService twitchUserEnsureService,
    ILogger<WeddingAnniversaryService> logger
)
{
    private static readonly (int Months, string Name)[] AnniversaryDefinitions =
    [
        (0, "Зелёная свадьба"),
        (12, "Ситцевая свадьба"),
        (24, "Бумажная свадьба"),
        (36, "Кожаная свадьба"),
        (48, "Льняная свадьба"),
        (60, "Деревянная свадьба"),
        (72, "Чугунная свадьба"),
        (78, "Цинковая свадьба"),
        (84, "Медная свадьба"),
        (96, "Жестяная свадьба"),
        (108, "Фаянсовая свадьба"),
        (120, "Оловянная свадьба (розовая)"),
        (132, "Стальная свадьба"),
        (150, "Никелевая свадьба"),
        (156, "Ландышевая (кружевная) свадьба"),
        (168, "Агатовая свадьба"),
        (180, "Стеклянная (хрустальная) свадьба"),
        (216, "Бирюзовая свадьба"),
        (240, "Фарфоровая свадьба"),
        (252, "Опаловая свадьба"),
        (264, "Бронзовая свадьба"),
        (276, "Берилловая свадьба"),
        (288, "Атласная свадьба"),
        (300, "Серебряная свадьба"),
        (360, "Жемчужная свадьба"),
        (420, "Коралловая свадьба"),
        (450, "Алюминиевая свадьба"),
        (456, "Ртутная свадьба"),
        (480, "Рубиновая свадьба"),
        (540, "Сапфировая свадьба"),
        (600, "Золотая свадьба"),
        (660, "Изумрудная свадьба"),
        (720, "Бриллиантовая свадьба"),
        (780, "Железная свадьба"),
        (810, "Кремниевая свадьба"),
        (840, "Благодатная свадьба"),
        (900, "Коронная свадьба"),
        (960, "Дубовая свадьба"),
        (1080, "Гранитная свадьба"),
        (1200, "Платиновая (красная) свадьба"),
    ];

    /// <summary>
    /// Получить следующую непосланную годовщину для пользователя (если она уже наступила).
    /// Возвращает null, если нет непосланных годовщин или свадебной даты.
    /// </summary>
    public async Task<(int Months, string Name)?> GetNextUnsentAnniversaryAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        (int Months, string Name)? result = null;

        if (!string.IsNullOrWhiteSpace(twitchId))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var user = await dbContext.TwitchUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.TwitchId == twitchId, cancellationToken);

                if (user?.WeddingDate is DateOnly weddingDate)
                {
                    var now = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3));
                    var today = DateOnly.FromDateTime(now.DateTime);

                    var lastMonths = user.LastWeddingCongratulatedMonths ?? -1;

                    foreach (var anniversary in AnniversaryDefinitions.OrderBy(d => d.Months))
                    {
                        if (anniversary.Months <= lastMonths)
                            continue;

                        var annDate = weddingDate.AddMonths(anniversary.Months);
                        if (annDate <= today)
                        {
                            result = anniversary;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Ошибка при поиске непосланной годовщины для пользователя {UserId}",
                    twitchId
                );
            }
        }

        return result;
    }

    /// <summary>
    /// Отметить годовщину как поздравленную
    /// </summary>
    public async Task MarkAnniversaryAsSentAsync(
        string twitchId,
        int months,
        CancellationToken cancellationToken = default
    )
    {
        if (!string.IsNullOrWhiteSpace(twitchId))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var user = await dbContext.TwitchUsers.FirstOrDefaultAsync(
                    u => u.TwitchId == twitchId,
                    cancellationToken
                );

                if (user != null)
                {
                    user.LastWeddingCongratulatedMonths = months;
                    user.LastWeddingCongratulatedOn = DateOnly.FromDateTime(
                        DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(3)).DateTime
                    );

                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Отмечена годовщина {Months} месяцев для пользователя {UserId}",
                        months,
                        twitchId
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Ошибка при отметке годовщины для пользователя {UserId}",
                    twitchId
                );
            }
        }
    }

    public async Task<OperationResult<TwitchUser>> SetWeddingDateAsync(
        string twitchId,
        DateOnly weddingDate,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<TwitchUser>.Bad("Не удалось сохранить дату свадьбы");

        if (!string.IsNullOrWhiteSpace(twitchId) && weddingDate != default)
        {
            try
            {
                await twitchUserEnsureService.EnsureUserExistsAsync(twitchId, cancellationToken);

                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var user = await dbContext.TwitchUsers.FirstOrDefaultAsync(
                    e => e.TwitchId == twitchId,
                    cancellationToken
                );

                if (user != null)
                {
                    user.WeddingDate = weddingDate;
                    user.LastWeddingCongratulatedOn = null;
                    user.LastWeddingCongratulatedMonths = null;

                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Для пользователя {UserId} сохранена дата свадьбы {WeddingDate}",
                        twitchId,
                        weddingDate
                    );

                    result = OperationResult<TwitchUser>.Ok("Дата свадьбы сохранена", user);
                }
                else
                {
                    result = OperationResult<TwitchUser>.Bad("Пользователь не найден");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при сохранении даты свадьбы для {UserId}", twitchId);
                result = OperationResult<TwitchUser>.Bad(
                    $"Ошибка при сохранении даты свадьбы: {ex.Message}"
                );
            }
        }
        else
        {
            result = OperationResult<TwitchUser>.Bad(
                "TwitchId и дата свадьбы должны быть заполнены"
            );
        }

        return result;
    }

    public async Task<OperationResult<TwitchUser>> ClearWeddingDateAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        var result = OperationResult<TwitchUser>.Bad("Не удалось удалить дату свадьбы");

        if (!string.IsNullOrWhiteSpace(twitchId))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );

                var user = await dbContext.TwitchUsers.FirstOrDefaultAsync(
                    e => e.TwitchId == twitchId,
                    cancellationToken
                );

                if (user != null)
                {
                    user.WeddingDate = null;
                    user.LastWeddingCongratulatedOn = null;
                    user.LastWeddingCongratulatedMonths = null;

                    await dbContext.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Для пользователя {UserId} очищена дата свадьбы",
                        twitchId
                    );

                    result = OperationResult<TwitchUser>.Ok("Дата свадьбы удалена", user);
                }
                else
                {
                    result = OperationResult<TwitchUser>.Bad("Пользователь не найден");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при удалении даты свадьбы для {UserId}", twitchId);
                result = OperationResult<TwitchUser>.Bad(
                    $"Ошибка при удалении даты свадьбы: {ex.Message}"
                );
            }
        }
        else
        {
            result = OperationResult<TwitchUser>.Bad("TwitchId не может быть пустым");
        }

        return result;
    }

    public static string BuildCongratulationMessageFromSpouse(
        string displayName,
        string spouseName,
        (int Months, string Name) anniversary
    )
    {
        var yearsText = FormatYears(anniversary.Months);
        var result = string.Empty;

        if (anniversary.Months == 0)
        {
            result =
                $"@{displayName}, твой супруг, {spouseName}, поздравляет тебя с {anniversary.Name}! Совет да любовь!";
        }
        else
        {
            result =
                $"@{displayName}, твой супруг, {spouseName}, поздравляет тебя с {anniversary.Name} ({yearsText} лет)! Совет да любовь!";
        }

        return result;
    }

    private static string FormatYears(int months)
    {
        var years = months / 12m;
        var result =
            years % 1 == 0
                ? ((int)years).ToString(CultureInfo.InvariantCulture)
                : years.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU"));

        return result;
    }
}
