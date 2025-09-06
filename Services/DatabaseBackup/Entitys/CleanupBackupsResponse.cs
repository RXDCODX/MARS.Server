namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
