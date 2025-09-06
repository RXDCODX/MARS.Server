namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель ответа с информацией о настройках pg_dump
/// </summary>
public class PgDumpSettingsResponse
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
    /// Настройки pg_dump
    /// </summary>
    public PgDumpSettings? Settings { get; set; }

    /// <summary>
    /// Информация о валидации пути
    /// </summary>
    public PgDumpValidationInfo? ValidationInfo { get; set; }
}
