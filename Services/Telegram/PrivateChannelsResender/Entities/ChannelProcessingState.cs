namespace MARS.Server.Services.Telegram.PrivateChannelsResender.Entities;

/// <summary>
/// Хранит состояние обработки сообщений для каждого канала
/// </summary>
public class ChannelProcessingState
{
    /// <summary>
    /// ID канала (Primary Key)
    /// </summary>
    [Key]
    public long ChannelId { get; set; }

    /// <summary>
    /// ID сообщения для offset-based пагинации (offset_id согласно Telegram API)
    /// </summary>
    public int OffsetId { get; set; }

    /// <summary>
    /// Хеш последних полученных сообщений для оптимизации (не требает перезагрузки если не изменились)
    /// </summary>
    public long? MessagesHash { get; set; }

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
