using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.DatabaseBackup.Models;

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
