namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
