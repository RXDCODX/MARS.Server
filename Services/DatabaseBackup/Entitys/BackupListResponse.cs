namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
