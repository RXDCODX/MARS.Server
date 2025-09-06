namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель запроса для обновления настроек pg_dump
/// </summary>
public class UpdatePgDumpSettingsRequest
{
    /// <summary>
    /// Путь к исполняемому файлу pg_dump
    /// </summary>
    [Required(ErrorMessage = "Путь к pg_dump обязателен")]
    [StringLength(500, ErrorMessage = "Путь к pg_dump не может превышать 500 символов")]
    public string PgDumpPath { get; set; } = string.Empty;

    /// <summary>
    /// Комментарий к настройкам
    /// </summary>
    [StringLength(1000, ErrorMessage = "Комментарий не может превышать 1000 символов")]
    public string? Comment { get; set; }
}
