using System.Globalization;
using System.Text.Json;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.YouTube;
using Microsoft.EntityFrameworkCore;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Services.Twitch.Rewards._39_MikuMonday;

/// <summary>
/// Сервис для управления треками Miku Monday
/// </summary>
public class MikuMondayTracksService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<MikuMondayTracksService> logger,
    IWebHostEnvironment environment,
    YouTubeResolver youTubeResolver
)
{
    private static readonly Lock Lock = new();
    private static bool _isInitialized = false;

    /// <summary>
    /// Загружает треки из miku.json в базу данных (выполняется один раз при старте)
    /// </summary>
    public async Task InitializeTracksAsync()
    {
        lock (Lock)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            // Проверяем есть ли уже треки в базе
            var existingCount = await db.MikuMondayTracks.AsNoTracking().CountAsync();

            if (existingCount > 0)
            {
                logger.LogInformation(
                    "Треки Miku уже загружены в базу данных: {Count} треков",
                    existingCount
                );
                return;
            }

            // Загружаем треки из JSON файла
            var jsonPath = Path.Combine(environment.ContentRootPath, "miku.json");

            if (!File.Exists(jsonPath))
            {
                logger.LogError("Файл miku.json не найден по пути: {Path}", jsonPath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var tracks = JsonSerializer.Deserialize<List<MikuTrackJson>>(
                jsonContent,
                JsonSerializerOptions.Web
            );

            if (tracks == null || tracks.Count == 0)
            {
                logger.LogError("Не удалось загрузить треки из miku.json");
                return;
            }

            // Добавляем треки в базу данных
            foreach (var track in tracks)
            {
                // Получаем информацию о треке через YouTubeResolver
                var baseTrackInfo = await youTubeResolver.ResolveVideoAsync(
                    track.Url,
                    CancellationToken.None
                );

                if (baseTrackInfo == null)
                {
                    logger.LogWarning(
                        "Не удалось получить информацию о треке #{Number}: {Url}",
                        track.Number,
                        track.Url
                    );
                    continue;
                }

                // Проверяем, существует ли уже такой трек в BaseTrackInfo
                var existingBaseTrack = await db
                    .SoundRequestBaseTrackInfos.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Url == baseTrackInfo.Url);

                Guid baseTrackInfoId;

                if (existingBaseTrack != null)
                {
                    baseTrackInfoId = existingBaseTrack.Id;
                }
                else
                {
                    // Добавляем новый BaseTrackInfo
                    db.SoundRequestBaseTrackInfos.Add(baseTrackInfo);
                    await db.SaveChangesAsync();
                    baseTrackInfoId = baseTrackInfo.Id;
                }

                // Создаем связующую запись MikuMondayTrack
                var mikuMondayTrack = new MikuMondayTrack
                {
                    Number = track.Number,
                    BaseTrackInfoId = baseTrackInfoId,
                };

                db.MikuMondayTracks.Add(mikuMondayTrack);
            }

            await db.SaveChangesAsync();

            logger.LogInformation("Загружено {Count} треков Miku в базу данных", tracks.Count);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
    }

    /// <summary>
    /// Получает случайный трек для пользователя
    /// </summary>
    public async Task<MikuMondayResult> GetRandomTrackForUserAsync(
        string twitchUserId,
        string displayName
    )
    {
        var result = new MikuMondayResult();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var weekInfo = GetCurrentWeekOfYear();

            // Проверяем, активировал ли пользователь уже награду в этот понедельник
            var existingActivation = await db
                .MikuMondayActivations.AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.TwitchUserId == twitchUserId
                    && a.Year == weekInfo.Year
                    && a.WeekOfYear == weekInfo.WeekOfYear
                );

            if (existingActivation != null)
            {
                result.Error = "Вы уже активировали Miku Monday в этот понедельник! 🎤";
                return result;
            }

            // Получаем все треки с информацией из BaseTrackInfo
            var allTracks = await db
                .MikuMondayTracks.AsNoTracking()
                .Include(mt => mt.BaseTrackInfo)
                .OrderBy(t => t.Number)
                .ToListAsync();

            if (allTracks.Count == 0)
            {
                result.Error = "Треки Miku не найдены в базе данных";
                logger.LogError(message: "{Error}", result.Error);
                return result;
            }

            // Получаем ID треков, которые уже выпали в этот понедельник
            var usedTrackIds = await db
                .MikuMondayActivations.AsNoTracking()
                .Where(a => a.Year == weekInfo.Year && a.WeekOfYear == weekInfo.WeekOfYear)
                .Select(a => a.MikuMondayTrackId)
                .ToListAsync();

            // Фильтруем доступные треки
            result.AvailableTracks = allTracks.Where(t => !usedTrackIds.Contains(t.Id)).ToList();

            if (result.AvailableTracks.Count == 0)
            {
                result.Error =
                    "Все треки уже разобраны в этот понедельник! 🎵 Попробуйте в следующий понедельник!";
                return result;
            }

            // Выбираем случайный трек
            var random = new Random();
            var randomIndex = random.Next(result.AvailableTracks.Count);
            result.Track = result.AvailableTracks[randomIndex];

            // Сохраняем активацию
            var activation = new MikuMondayActivation
            {
                TwitchUserId = twitchUserId,
                DisplayName = displayName,
                MikuMondayTrackId = result.Track.Id,
                Year = weekInfo.Year,
                WeekOfYear = weekInfo.WeekOfYear,
            };

            db.MikuMondayActivations.Add(activation);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Пользователь {DisplayName} получил трек #{Number}: {TrackName}",
                displayName,
                result.Track.Number,
                result.Track.BaseTrackInfo?.TrackName ?? "Unknown"
            );

            // Обновляем список доступных треков (убираем выбранный)
            result.AvailableTracks.Remove(result.Track);
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result.Error = "Произошла ошибка при получении трека";
        }

        return result;
    }

    /// <summary>
    /// Получает случайный трек для стримера без списания из очереди
    /// </summary>
    public async Task<MikuMondayResult> GetRandomTrackForStreamerAsync()
    {
        var result = new MikuMondayResult();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var allTracks = await db
                .MikuMondayTracks.AsNoTracking()
                .Include(mt => mt.BaseTrackInfo)
                .OrderBy(t => t.Number)
                .ToListAsync();

            if (allTracks.Count > 0)
            {
                var weekInfo = GetCurrentWeekOfYear();
                var usedTrackIds = await db
                    .MikuMondayActivations.AsNoTracking()
                    .Where(a => a.Year == weekInfo.Year && a.WeekOfYear == weekInfo.WeekOfYear)
                    .Select(a => a.MikuMondayTrackId)
                    .ToListAsync();

                var preferredTracks = allTracks.Where(t => !usedTrackIds.Contains(t.Id)).ToList();
                var randomPool = preferredTracks.Count > 0 ? preferredTracks : allTracks;
                var random = new Random();
                var selectedTrack = randomPool[random.Next(randomPool.Count)];

                result.Track = selectedTrack;
                result.AvailableTracks =
                    preferredTracks.Count > 0
                        ? preferredTracks.Where(t => t.Id != selectedTrack.Id).ToList()
                        : allTracks.Where(t => t.Id != selectedTrack.Id).ToList();
            }
            else
            {
                result.Error = "Треки Miku не найдены в базе данных";
                logger.LogError(message: "{Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            result.Error = "Произошла ошибка при получении трека";
        }

        return result;
    }

    /// <summary>
    /// Получает список доступных треков для текущего понедельника
    /// </summary>
    public async Task<List<MikuMondayTrack>> GetAvailableTracksAsync()
    {
        var result = new List<MikuMondayTrack>();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var weekInfo = GetCurrentWeekOfYear();

            // Получаем все треки
            var allTracks = await db
                .MikuMondayTracks.AsNoTracking()
                .Include(mt => mt.BaseTrackInfo)
                .OrderBy(t => t.Number)
                .ToListAsync();

            // Получаем ID треков, которые уже выпали в этот понедельник
            var usedTrackIds = await db
                .MikuMondayActivations.AsNoTracking()
                .Where(a => a.Year == weekInfo.Year && a.WeekOfYear == weekInfo.WeekOfYear)
                .Select(a => a.MikuMondayTrackId)
                .ToListAsync();

            // Фильтруем доступные треки
            result = allTracks.Where(t => !usedTrackIds.Contains(t.Id)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }

        return result;
    }

    /// <summary>
    /// Получает текущий год и номер недели
    /// </summary>
    private static WeekInfo GetCurrentWeekOfYear()
    {
        var result = new WeekInfo();

        var now = DateTime.Now;
        var calendar = CultureInfo.CurrentCulture.Calendar;

        result.Year = now.Year;
        result.WeekOfYear = calendar.GetWeekOfYear(
            now,
            CalendarWeekRule.FirstDay,
            DayOfWeek.Monday
        );

        return result;
    }

    /// <summary>
    /// Информация о текущей неделе
    /// </summary>
    private class WeekInfo
    {
        public int Year { get; set; }
        public int WeekOfYear { get; set; }
    }

    /// <summary>
    /// Класс для десериализации JSON
    /// </summary>
    public class MikuTrackJson
    {
        public int Number { get; set; }
        public required string Artist { get; set; }
        public required string Title { get; set; }
        public required string Url { get; set; }
    }
}
