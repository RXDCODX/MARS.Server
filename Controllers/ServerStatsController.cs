using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.ServiceManager.Entitys;
using MARS.Server.Services.SoundBarService.Entitys;
using MARS.Server.Services.Twitch.Client;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.PuntoSwitcher;
using MARS.Server.Services.Twitch.Synthesizer;
using MARS.Server.Services.Twitch.WeddingAnniversary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Controllers;

/// <summary>
/// Контроллер для получения статистики сервера
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServerStatsController(
    IServiceManager serviceManager,
    ILogger<ServerStatsController> logger,
    EventSubService eventSubService,
    TwitchConnectionManager twitchConnectionManager,
    ISoundBar soundBar,
    WeddingAnniversaryService weddingAnniversaryService
) : ControllerBase
{
    private static readonly Stopwatch UptimeStopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Получить статистику сервера
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OperationResult<ServerStatsResponse>>> GetStats(
        CancellationToken cancellationToken = default
    )
    {
        ActionResult<OperationResult<ServerStatsResponse>> result;
        try
        {
            var process = Process.GetCurrentProcess();
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var workingSet = process.WorkingSet64;
            var privateMemory = process.PrivateMemorySize64;
            var gcHeap = GC.GetTotalMemory(forceFullCollection: false);
            var cpuUsage = GetCpuUsage(process);

            var services = await serviceManager.GetAllServicesAsync();
            var serviceList = services.ToList();

            var audioControllerConnected = await soundBar.CheckHealthAsync();
            var nearestAnniversary = await weddingAnniversaryService.GetNearestAnniversaryAsync(
                cancellationToken
            );

            var stats = new ServerStatsResponse
            {
                CpuUsagePercent = cpuUsage,
                MemoryWorkingSetBytes = workingSet,
                MemoryPrivateBytes = privateMemory,
                MemoryGcHeapBytes = gcHeap,
                MemoryTotalBytes = totalMemory,
                UptimeSeconds = UptimeStopwatch.Elapsed.TotalSeconds,
                ThreadCount = process.Threads.Count,
                ActiveServicesCount = serviceList.Count(s => s.Status == ServiceStatus.Running),
                TotalServicesCount = serviceList.Count,
                OsVersion = Environment.OSVersion.ToString(),
                RuntimeVersion = RuntimeInformation.FrameworkDescription,
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                IsEventSubConnected = eventSubService.IsWebSocketConnected,
                IsTwitchChatConnected = twitchConnectionManager.IsConnected,
                IsAudioControllerConnected = audioControllerConnected,
                IsTtsConnected = audioControllerConnected,
                IsPuntoSwitcherEnabled = PuntoSwitcherState.IsFilterEnabled,
                NearestWeddingAnniversaryName = nearestAnniversary?.AnniversaryName,
                NearestWeddingAnniversaryDate = nearestAnniversary?.AnniversaryDate,
                NearestWeddingAnniversaryUser = nearestAnniversary?.DisplayName,
            };

            result = Ok(
                OperationResult<ServerStatsResponse>.Ok("Статистика сервера получена", stats)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении статистики сервера");
            result = Ok(
                OperationResult<ServerStatsResponse>.Bad(
                    "Ошибка при получении статистики сервера",
                    new ServerStatsResponse()
                )
            );
        }

        return result;
    }

    private static double GetCpuUsage(Process process)
    {
        try
        {
            var cpuTime = process.TotalProcessorTime;
            var uptime = process.ExitTime == DateTime.MaxValue
                ? DateTime.UtcNow - process.StartTime.ToUniversalTime()
                : process.ExitTime - process.StartTime;

            if (uptime.TotalMilliseconds <= 0)
            {
                return 0;
            }

            return Math.Round(
                (cpuTime.TotalMilliseconds / (uptime.TotalMilliseconds * Environment.ProcessorCount))
                    * 100,
                2
            );
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Ответ со статистикой сервера
/// </summary>
public class ServerStatsResponse
{
    /// <summary>
    /// Использование CPU (%)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Рабочий набор памяти (байты)
    /// </summary>
    public long MemoryWorkingSetBytes { get; set; }

    /// <summary>
    /// Приватная память (байты)
    /// </summary>
    public long MemoryPrivateBytes { get; set; }

    /// <summary>
    /// GC Heap (байты)
    /// </summary>
    public long MemoryGcHeapBytes { get; set; }

    /// <summary>
    /// Общий объём доступной памяти (байты)
    /// </summary>
    public long MemoryTotalBytes { get; set; }

    /// <summary>
    /// Время работы (секунды)
    /// </summary>
    public double UptimeSeconds { get; set; }

    /// <summary>
    /// Количество потоков
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Количество активных сервисов
    /// </summary>
    public int ActiveServicesCount { get; set; }

    /// <summary>
    /// Общее количество сервисов
    /// </summary>
    public int TotalServicesCount { get; set; }

    /// <summary>
    /// Версия ОС
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Версия runtime
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Имя машины
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Количество процессоров
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Подключен ли WebSocket EventSub к Twitch
    /// </summary>
    public bool IsEventSubConnected { get; set; }

    /// <summary>
    /// Подключен ли Twitch Chat
    /// </summary>
    public bool IsTwitchChatConnected { get; set; }

    /// <summary>
    /// Подключен ли AudioController (SoundBar)
    /// </summary>
    public bool IsAudioControllerConnected { get; set; }

    /// <summary>
    /// Подключен ли AudioController (TTS)
    /// </summary>
    public bool IsTtsConnected { get; set; }

    /// <summary>
    /// Включён ли PuntoSwitcher для чата
    /// </summary>
    public bool IsPuntoSwitcherEnabled { get; set; }

    /// <summary>
    /// Название ближайшей годовщины свадьбы
    /// </summary>
    public string? NearestWeddingAnniversaryName { get; set; }

    /// <summary>
    /// Дата ближайшей годовщины свадьбы
    /// </summary>
    public DateTimeOffset? NearestWeddingAnniversaryDate { get; set; }

    /// <summary>
    /// Пользователь ближайшей годовщины свадьбы
    /// </summary>
    public string? NearestWeddingAnniversaryUser { get; set; }
}
