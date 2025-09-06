namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель настроек pg_dump для хранения в базе данных
/// </summary>
public class PgDumpSettings
{
    /// <summary>
    /// Уникальный идентификатор настроек
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Путь к исполняемому файлу pg_dump
    /// </summary>
    [Required(ErrorMessage = "Путь к pg_dump обязателен")]
    [StringLength(500, ErrorMessage = "Путь к pg_dump не может превышать 500 символов")]
    public string PgDumpPath { get; set; } = string.Empty;

    /// <summary>
    /// Время создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Комментарий к настройкам
    /// </summary>
    [StringLength(1000, ErrorMessage = "Комментарий не может превышать 1000 символов")]
    public string? Comment { get; set; }

    /// <summary>
    /// Флаг активности настроек (только одна запись может быть активной)
    /// </summary>
    public bool IsActive { get; set; } = true;
}
