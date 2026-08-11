using System.Text.Json;
using MARS.Server.Hubs;
using MARS.Server.Hubs.Interfaces;
using MARS.Server.Services;
using MARS.Server.Services.PyroAlerts.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MARS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestAlertsController(
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IWebHostEnvironment env
) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [HttpPost("alert")]
    public async Task<ActionResult<OperationResult>> SendAlert([FromBody] MediaDto dto)
    {
        if (
            dto.MediaInfo.MetaInfo.IsFreezeRequired
            && dto.MediaInfo.MetaInfo.Priority != MediaAlertPriority.High
        )
        {
            ActionResult<OperationResult> badResult = Ok(
                OperationResult.Bad("IsFreezeRequired может быть true только когда Priority = High")
            );
            return badResult;
        }

        await hubContext.Clients.All.Alert(dto);

        ActionResult<OperationResult> result = Ok(OperationResult.Ok("Alert sent"));
        return result;
    }

    [HttpPost("alerts-batch")]
    public async Task<ActionResult<OperationResult>> SendAlertsBatch([FromBody] MediaDto[] dtos)
    {
        foreach (var dto in dtos)
        {
            if (
                dto.MediaInfo.MetaInfo.IsFreezeRequired
                && dto.MediaInfo.MetaInfo.Priority != MediaAlertPriority.High
            )
            {
                ActionResult<OperationResult> badResult = Ok(
                    OperationResult.Bad(
                        "IsFreezeRequired может быть true только когда Priority = High"
                    )
                );
                return badResult;
            }
        }

        await hubContext.Clients.All.Alerts(dtos);

        ActionResult<OperationResult> result = Ok(OperationResult.Ok($"{dtos.Length} alerts sent"));
        return result;
    }

    [HttpPost("alert-by-type")]
    public async Task<ActionResult<OperationResult<MediaDto>>> SendAlertByType(
        [FromQuery] MediaType type,
        [FromQuery] MediaAlertPriority priority = MediaAlertPriority.Normal,
        [FromQuery] int duration = 5,
        [FromQuery] string? text = null
    )
    {
        var mediaInfo = CreateTestMediaInfo(type, priority, duration, text);

        if (mediaInfo == null)
        {
            ActionResult<OperationResult<MediaDto>> badResult = Ok(
                OperationResult<MediaDto>.Bad($"Не удалось создать алерт для типа {type}")
            );
            return badResult;
        }

        var dto = new MediaDto(mediaInfo) { MediaInfo = mediaInfo };
        await hubContext.Clients.All.Alert(dto);

        ActionResult<OperationResult<MediaDto>> result = Ok(
            OperationResult<MediaDto>.Ok($"Alert sent: {type}", dto)
        );
        return result;
    }

    [HttpGet("settings")]
    public ActionResult<OperationResult<List<AlertSettingsEntry>>> GetAlertSettings()
    {
        var settingsPath = Path.Combine(env.WebRootPath, "Alerts", "settings.json");

        if (!System.IO.File.Exists(settingsPath))
        {
            ActionResult<OperationResult<List<AlertSettingsEntry>>> notFound = Ok(
                OperationResult<List<AlertSettingsEntry>>.Bad("settings.json не найден")
            );
            return notFound;
        }

        var json = System.IO.File.ReadAllText(settingsPath);
        var entries = JsonSerializer.Deserialize<List<AlertSettingsEntry>>(json, JsonOptions) ?? [];

        ActionResult<OperationResult<List<AlertSettingsEntry>>> result = Ok(
            OperationResult<List<AlertSettingsEntry>>.Ok("Настройки алертов получены", entries)
        );
        return result;
    }

    [HttpGet("available-files")]
    public ActionResult<OperationResult<Dictionary<MediaType, string[]>>> GetAvailableFiles()
    {
        var alertsDir = Path.Combine(env.WebRootPath, "Alerts");
        var resultDict = new Dictionary<MediaType, string[]>();

        if (Directory.Exists(alertsDir))
        {
            var imageFiles = Directory
                .GetFiles(alertsDir, "*.*")
                .Where(f =>
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                )
                .Select(f => "Alerts/" + Path.GetFileName(f))
                .ToArray();

            var videoFiles = Directory
                .GetFiles(alertsDir, "*.*")
                .Where(f =>
                    f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                )
                .Select(f => "Alerts/" + Path.GetFileName(f))
                .ToArray();

            var audioFiles = Directory
                .GetFiles(alertsDir, "*.*")
                .Where(f =>
                    f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                )
                .Select(f => "Alerts/" + Path.GetFileName(f))
                .ToArray();

            if (imageFiles.Length > 0)
            {
                resultDict[MediaType.Image] = imageFiles;
            }

            if (videoFiles.Length > 0)
            {
                resultDict[MediaType.Video] = videoFiles;
            }

            if (audioFiles.Length > 0)
            {
                resultDict[MediaType.Audio] = audioFiles;
            }
        }

        var facesDir = Path.Combine(env.WebRootPath, "faces");
        if (Directory.Exists(facesDir))
        {
            var gifFiles = Directory
                .GetFiles(facesDir, "*.gif")
                .Select(f => "faces/" + Path.GetFileName(f))
                .ToArray();

            if (gifFiles.Length > 0)
            {
                resultDict[MediaType.Gif] = gifFiles;
            }
        }

        ActionResult<OperationResult<Dictionary<MediaType, string[]>>> result = Ok(
            OperationResult<Dictionary<MediaType, string[]>>.Ok(
                "Доступные файлы получены",
                resultDict
            )
        );
        return result;
    }

    private MediaInfo? CreateTestMediaInfo(
        MediaType type,
        MediaAlertPriority priority,
        int duration,
        string? text
    )
    {
        var filePath = FindFileForType(type);
        if (filePath == null)
        {
            return null;
        }

        var extension = Path.GetExtension(filePath).TrimStart('.');
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        var mediaInfo = new MediaInfo
        {
            TextInfo = new MediaTextInfo
            {
                Text = text ?? string.Empty,
                TextColor = null,
                KeyWordsColor = null,
            },
            FileInfo = new MediaFileInfo
            {
                Type = type,
                FilePath = filePath,
                IsLocalFile = true,
                FileName = fileName,
                Extension = extension,
            },
            PositionInfo = new MediaPositionInfo
            {
                IsProportion = true,
                IsResizeRequires = false,
                Width = 500,
                Height = 500,
                IsRotated = false,
                Rotation = 0,
                XCoordinate = 0,
                YCoordinate = 0,
                RandomCoordinates = false,
                IsVerticallCenter = false,
                IsHorizontalCenter = false,
                IsUseOriginalWidthAndHeight = true,
            },
            MetaInfo = new MediaMetaInfo
            {
                DisplayName = "TestUser",
                Duration = duration,
                Priority = priority,
                IsLooped = false,
                Volume = 100,
            },
            StylesInfo = new MediaStylesInfo { IsBorder = false, IsShowLetterbox = false },
        };

        return mediaInfo;
    }

    private string? FindFileForType(MediaType type)
    {
        var root = env.WebRootPath;

        return type switch
        {
            MediaType.Image => FindFirstFile(root, "Alerts", "*.jpg", "*.png"),
            MediaType.Video => FindFirstFile(root, "Alerts", "*.webm", "*.mp4"),
            MediaType.Audio => FindFirstFile(root, "Alerts", "*.mp3", "*.wav"),
            MediaType.Gif => FindFirstFile(root, "faces", "*.gif"),
            MediaType.Voice => FindFirstFile(root, "Alerts", "*.wav"),
            _ => null,
        };
    }

    private static string? FindFirstFile(string root, string subDir, params string[] patterns)
    {
        var dir = Path.Combine(root, subDir);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return subDir + "/" + Path.GetRelativePath(dir, files[0]).Replace('\\', '/');
            }
        }

        return null;
    }

    public class AlertSettingsEntry
    {
        public int TwitchPointsCost { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Duration { get; set; }
        public bool RandomCoordinates { get; set; }
        public int XCoordinate { get; set; }
        public int YCoordinate { get; set; }
        public int Type { get; set; }
        public string TextPosition { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string TextColor { get; set; } = string.Empty;
        public string KeyWordsColor { get; set; } = string.Empty;
        public bool VIP { get; set; }
        public bool IsBorder { get; set; }
        public bool IsProportion { get; set; }
        public bool IsResizeRequires { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsRotated { get; set; }
        public int Rotation { get; set; }
        public bool IsLooped { get; set; }
    }
}
