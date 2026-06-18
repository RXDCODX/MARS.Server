using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.DataBaseContext;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MARS.Server.Services.Twitch.WeddingAnniversary;

public class WeddingAnniversaryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
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
    /// Получить ближайшую годовщину среди всех женатых пользователей,
    /// которую ещё не поздравляли и дата которой уже наступила (или наступает сегодня).
    /// Неотмеченные (неженатые) пользователи не учитываются.
    /// Возвращает null, если нет подходящих годовщин.
    /// </summary>
    public virtual async Task<NearestAnniversaryDto?> GetNearestAnniversaryAsync(
        CancellationToken cancellationToken = default
    )
    {
        NearestAnniversaryDto? result = null;

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var marriedUsers = await dbContext
                .Husbands.AsNoTracking()
                .Include(h => h.TwitchUser)
                .Where(h => h.IsPrivated && h.WhenPrivated != null)
                .ToListAsync(cancellationToken);

            var today = DateTimeOffset.Now.ToLocalTime().Date;
            NearestAnniversaryDto? nearest = null;
            DateTimeOffset? nearestDate = null;

            foreach (var user in marriedUsers)
            {
                if (user.WhenPrivated is null)
                {
                    continue;
                }

                var weddingDate = user.WhenPrivated.Value;
                var lastMonths = user.LastWeddingCongratulatedMonths ?? -1;

                foreach (var anniversary in AnniversaryDefinitions.OrderBy(d => d.Months))
                {
                    if (anniversary.Months <= lastMonths)
                    {
                        continue;
                    }

                    var anniversaryDate = weddingDate.AddMonths(anniversary.Months);
                    if (anniversaryDate.Date >= today)
                    {
                        if (nearestDate is null || anniversaryDate < nearestDate)
                        {
                            nearestDate = anniversaryDate;
                            nearest = new NearestAnniversaryDto
                            {
                                TwitchId = user.TwitchId,
                                DisplayName =
                                    user.TwitchUser?.DisplayName ?? user.TwitchId,
                                AnniversaryName = anniversary.Name,
                                AnniversaryDate = anniversaryDate,
                                Months = anniversary.Months,
                            };
                        }

                        break;
                    }
                }
            }

            result = nearest;

            if (result != null)
            {
                logger.LogInformation(
                    "Найдена ближайшая годовщина: {User} - {Name} ({Date})",
                    result.DisplayName,
                    result.AnniversaryName,
                    result.AnniversaryDate
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ошибка при поиске ближайшей годовщины среди всех пользователей"
            );
        }

        return result;
    }

    /// <summary>
    /// Получить следующую непосланную годовщину для пользователя (если она уже наступила).
    /// Возвращает null, если нет непосланных годовщин или даты свадьбы.
    /// </summary>
    public virtual async Task<(int Months, string Name)?> GetNextUnsentAnniversaryAsync(
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

                var host = await dbContext
                    .Husbands.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.TwitchId == twitchId, cancellationToken);

                if (host is { IsPrivated: true, WhenPrivated: { } weddingDate })
                {
                    var now = DateTimeOffset.Now.ToLocalTime();
                    var today = now.Date;

                    var lastMonths = host.LastWeddingCongratulatedMonths ?? -1;

                    foreach (var anniversary in AnniversaryDefinitions.OrderBy(d => d.Months))
                    {
                        if (anniversary.Months <= lastMonths)
                        {
                            continue;
                        }

                        var anniversaryDate = weddingDate.AddMonths(anniversary.Months);
                        if (anniversaryDate.Date <= today)
                        {
                            result = anniversary;
                            break;
                        }
                    }

                    if (result.HasValue)
                    {
                        // Отмечаем годовщину как отправленную
                        await MarkAnniversaryAsSentAsync(
                            twitchId,
                            result.Value.Months,
                            cancellationToken
                        );
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
    public virtual async Task MarkAnniversaryAsSentAsync(
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

                var host = await dbContext.Husbands.FindAsync([twitchId], cancellationToken);

                host?.LastWeddingCongratulatedMonths = months;

                if (host != null)
                {
                    dbContext.Husbands.Update(host);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Отмечена годовщина {Months} месяцев для пользователя {UserId}",
                    months,
                    twitchId
                );
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

    public static string BuildCongratulationMessageFromSpouse(
        string displayName,
        string spouseName,
        (int Months, string Name) anniversary
    )
    {
        var yearsText = FormatYears(anniversary.Months);
        var declined = DeclineAnniversaryName(anniversary.Name);

        var result = string.Empty;

        if (anniversary.Months == 0)
        {
            result =
                $"@{displayName}, твой супруг, {spouseName}, поздравляет тебя с {declined}! Совет да любовь!";
        }
        else
        {
            result =
                $"@{displayName}, твой супруг, {spouseName}, поздравляет тебя с {declined} ({yearsText} лет)! Совет да любовь!";
        }

        return result;
    }

    private static string DeclineAnniversaryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        // Простое правило: заменить "свадьба" на "свадьбой", а прилагательное
        // перед словом "свадьбой" склонить из "-ая" -> "-ой" (и "-ная" -> "-ной").
        var result = Regex.Replace(name, "\\bсвадьба\\b", "свадьбой", RegexOptions.IgnoreCase);

        // Склоняем прилагательное перед "свадьбой"
        result = Regex.Replace(
            result,
            "([А-Яа-яЁё]+)ная\\s+свадьбой",
            "$1ной свадьбой",
            RegexOptions.IgnoreCase
        );
        result = Regex.Replace(
            result,
            "([А-Яа-яЁё]+)ая\\s+свадьбой",
            "$1ой свадьбой",
            RegexOptions.IgnoreCase
        );

        // Также обработать случаи с закрывающей скобкой перед "свадьбой": e.g. "Стеклянная (хрустальная) свадьба"
        result = Regex.Replace(
            result,
            "([А-Яа-яЁё]+)ная(?=\\s*\\()",
            "$1ной",
            RegexOptions.IgnoreCase
        );
        result = Regex.Replace(
            result,
            "([А-Яа-яЁё]+)ая(?=\\s*\\()",
            "$1ой",
            RegexOptions.IgnoreCase
        );

        // Склоняем прилагательные внутри скобок: (хрустальная) -> (хрустальной)
        result = Regex.Replace(
            result,
            "\\(\\s*([А-Яа-яЁё]+)ная\\b",
            "($1ной",
            RegexOptions.IgnoreCase
        );
        result = Regex.Replace(
            result,
            "\\(\\s*([А-Яа-яЁё]+)ая\\b",
            "($1ой",
            RegexOptions.IgnoreCase
        );

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

public class NearestAnniversaryDto
{
    public required string TwitchId { get; set; }
    public required string DisplayName { get; set; }
    public required string AnniversaryName { get; set; }
    public DateTimeOffset AnniversaryDate { get; set; }
    public int Months { get; set; }
}
