namespace MARS.Server.Services.StreamAcrhive.Entitys;

public class StreamArchiveFile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Ссылка на конфигурацию архивирования
    /// </summary>
    public Guid ConfigId { get; set; }

    /// <summary>
    /// Оригинальное имя файла
    /// </summary>
    public string OriginalFileName { get; set; } = null!;

    /// <summary>
    /// Имя файла после обработки
    /// </summary>
    public string ProcessedFileName { get; set; } = null!;

    /// <summary>
    /// Полный путь к оригинальному файлу
    /// </summary>
    public string OriginalFilePath { get; set; } = null!;

    /// <summary>
    /// Размер оригинального файла в байтах
    /// </summary>
    public long OriginalFileSize { get; set; }

    /// <summary>
    /// Дата и время обнаружения файла
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    /// <summary>
    /// Дата и время начала обработки
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Дата и время завершения обработки
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Статус обработки файла
    /// </summary>
    public StreamArchiveFileStatus Status { get; set; }

    /// <summary>
    /// Количество частей, на которые был разбит файл (если больше 1)
    /// </summary>
    public int ChunksCount { get; set; }

    /// <summary>
    /// Сообщение об ошибке (если обработка не удалась)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// ID сообщения в Telegram (если файл загружен как есть)
    /// </summary>
    public long? TelegramMessageId { get; set; }

    /// <summary>
    /// Список частей файла
    /// </summary>
    public virtual ICollection<StreamArchiveFileChunk> Chunks { get; set; } = [];

    /// <summary>
    /// Ссылка на конфигурацию
    /// </summary>
    public virtual StreamArchiveConfig Config { get; set; } = null!;
}
