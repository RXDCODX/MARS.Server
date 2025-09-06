namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
