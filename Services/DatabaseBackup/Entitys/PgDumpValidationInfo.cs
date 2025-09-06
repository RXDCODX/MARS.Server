namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель информации о валидации pg_dump
/// </summary>
public class PgDumpValidationInfo
{
    /// <summary>
    /// Файл существует по указанному пути
    /// </summary>
    public bool FileExists { get; set; }

    /// <summary>
    /// Версия pg_dump
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Сообщение о валидации
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Время последней проверки
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
