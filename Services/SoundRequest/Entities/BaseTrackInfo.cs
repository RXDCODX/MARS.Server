using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Entities;

public class BaseTrackInfo
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public required string TrackName { get; set; }

    public string[]? Authors { get; set; }

    public TimeSpan Duration { get; set; }

    public required Uri Url { get; init; }

    public DateTime LastTimePlays { get; set; } = DateTime.UnixEpoch;

    public string? ArtworkUrl { get; set; }

    public string? VideoId { get; set; }

    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Порядок в очереди (0 = текущий трек, null = не в очереди)
    /// </summary>
    public int? QueueOrder { get; set; }

    /// <summary>
    /// Twitch ID пользователя, заказавшего трек
    /// </summary>
    [MaxLength(50)]
    public string? RequestedByTwitchId { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch, заказавшего трек
    /// </summary>
    [ForeignKey(nameof(RequestedByTwitchId))]
    public TwitchUser? RequestedByTwitchUser { get; set; }

    [NotMapped]
    public string Title
    {
        get
        {
            if (Authors is { Length: > 0 })
            {
                var authors = string.Join(',', Authors);
                return string.Concat(authors, ' ', '-', ' ', TrackName);
            }

            return TrackName;
        }
    }
}
