namespace MARS.Server.Services.DatabaseBackup.Entitys;

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
