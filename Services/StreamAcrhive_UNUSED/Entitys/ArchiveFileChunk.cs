namespace MARS.Server.Services.StreamAcrhive.Entitys;

public class StreamArchiveFileChunk
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Ссылка на родительский файл
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Номер части (начиная с 1)
    /// </summary>
    public int ChunkNumber { get; set; }

    /// <summary>
    /// Общее количество частей
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Имя файла части
    /// </summary>
    public string ChunkFileName { get; set; } = null!;

    /// <summary>
    /// Размер части в байтах
    /// </summary>
    public long ChunkSize { get; set; }

    /// <summary>
    /// Смещение в оригинальном файле
    /// </summary>
    public long OffsetInOriginalFile { get; set; }

    /// <summary>
    /// Дата и время загрузки части
    /// </summary>
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// ID сообщения в Telegram
    /// </summary>
    public long? TelegramMessageId { get; set; }

    /// <summary>
    /// Статус загрузки части
    /// </summary>
    public StreamArchiveChunkStatus Status { get; set; }

    /// <summary>
    /// Сообщение об ошибке (если загрузка не удалась)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Ссылка на родительский файл
    /// </summary>
    public virtual StreamArchiveFile File { get; set; } = null!;
}
