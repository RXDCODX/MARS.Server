namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
