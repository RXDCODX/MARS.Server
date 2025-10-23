using MARS.Server.Services.Twitch.Entitys;

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
    /// Порядок в очереди (0 = текущий трек, null = не в очереди)
    /// </summary>
    public int? QueueOrder { get; set; }

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

    /// <summary>
    /// Элемент помечен как удаленный (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
