using MARS.Server.Services.Twitch.Entitys;
using TL;

namespace MARS.Server.Services.SoundRequest.Entities;

/// <summary>
/// Элемент очереди - заказ пользователя на воспроизведение трека
/// </summary>
public class QueueItem
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// ID трека
    /// </summary>
    [Required]
    public required Guid TrackId { get; set; }

    /// <summary>
    /// Ссылка на трек
    /// </summary>
    [ForeignKey(nameof(TrackId))]
    public BaseTrackInfo? Track { get; set; }

    /// <summary>
    /// Порядок в очереди (уникальное значение):
    /// 0 - текущий трек, который будет проигран при следующем сдвиге
    /// > 0 - треки в очереди (1, 2, 3...), новые треки добавляются с максимальным значением + 1
    /// &lt; 0 - проигранные треки/история (-1, -2, -3..., где -1 = последний проигранный)
    /// При начале воспроизведения вся очередь сдвигается на -1
    /// </summary>
    [Required]
    public required int QueueOrder { get; set; }

    /// <summary>
    /// Twitch ID пользователя, заказавшего трек
    /// </summary>
    [MaxLength(50)]
    [Required]
    public required string RequestedByTwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch, заказавшего трек
    /// </summary>
    [ForeignKey(nameof(RequestedByTwitchId))]
    public TwitchUser? RequestedByTwitchUser { get; set; }

    /// <summary>
    /// Дата и время заказа трека
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
