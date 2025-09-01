namespace MARS.Server.Services.DatabaseBackup.Models;

/// <summary>
/// Модель для создания резервной копии
/// </summary>
public class CreateBackupRequest
{
    /// <summary>
    /// Имя базы данных для резервного копирования
    /// </summary>
    [Required(ErrorMessage = "Имя базы данных обязательно")]
    [RegularExpression(
        "^(dev|prod)$",
        ErrorMessage = "Поддерживаются только базы данных 'dev' и 'prod'"
    )]
    public string DatabaseName { get; set; } = string.Empty;
}

/// <summary>
/// Модель ответа при создании резервной копии
/// </summary>
public class CreateBackupResponse
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение о результате
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// URL для скачивания резервной копии
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Имя файла
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Модель для скачивания резервной копии
/// </summary>
public class DownloadBackupRequest
{
    /// <summary>
    /// Имя файла резервной копии
    /// </summary>
    [Required(ErrorMessage = "Имя файла обязательно")]
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Модель информации о файле резервной копии
/// </summary>
public class BackupFileInfo
{
    /// <summary>
    /// Имя файла
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Имя базы данных
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Размер файла в МБ
    /// </summary>
    public double SizeMB => Math.Round(Size / (1024.0 * 1024.0), 2);

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// MIME-тип файла
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// URL для скачивания
    /// </summary>
    public string DownloadUrl => $"/memory/{FileName}";
}

/// <summary>
/// Модель ответа со списком резервных копий
/// </summary>
public class BackupListResponse
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Список резервных копий
    /// </summary>
    public List<BackupFileInfo> Backups { get; set; } = [];

    /// <summary>
    /// Общее количество
    /// </summary>
    public int TotalCount { get; set; }
}

/// <summary>
/// Модель для очистки старых резервных копий
/// </summary>
public class CleanupBackupsRequest
{
    /// <summary>
    /// Количество копий для сохранения
    /// </summary>
    [Range(1, 100, ErrorMessage = "Количество должно быть от 1 до 100")]
    public int KeepCount { get; set; } = 10;
}

/// <summary>
/// Модель ответа при очистке
/// </summary>
public class CleanupBackupsResponse
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение о результате
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Количество удаленных файлов
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Количество сохраненных файлов
    /// </summary>
    public int KeepCount { get; set; }
}

/// <summary>
/// Модель статуса резервного копирования
/// </summary>
public class BackupStatusInfo
{
    /// <summary>
    /// Общее количество резервных копий
    /// </summary>
    public int TotalBackups { get; set; }

    /// <summary>
    /// Общий размер в байтах
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Общий размер в МБ
    /// </summary>
    public double TotalSizeMB { get; set; }

    /// <summary>
    /// Самая старая резервная копия
    /// </summary>
    public DateTime? OldestBackup { get; set; }

    /// <summary>
    /// Самая новая резервная копия
    /// </summary>
    public DateTime? NewestBackup { get; set; }

    /// <summary>
    /// Информация о хранилище
    /// </summary>
    public string StorageInfo { get; set; } = "MemoryStorage";
}

/// <summary>
/// Модель ответа со статусом
/// </summary>
public class BackupStatusResponse
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Информация о статусе
    /// </summary>
    public BackupStatusInfo Status { get; set; } = new();
}
