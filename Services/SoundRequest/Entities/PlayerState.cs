using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Entities;

public class PlayerState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// ID текущего трека (foreign key)
    /// </summary>
    public Guid? CurrentTrackId { get; set; }

    /// <summary>
    /// ID следующего трека (foreign key)
    /// </summary>
    public Guid? NextTrackId { get; set; }

    public TimeSpan? CurrentTrackProgress { get; set; }

    /// <summary>
    /// Текущее состояние воспроизведения плеера
    /// </summary>
    public PlaybackState State { get; set; } = PlaybackState.Stopped;

    /// <summary>
    /// Звук выключен (независимо от состояния воспроизведения)
    /// </summary>
    public bool IsMuted { get; set; }

    public float Volume { get; set; } = 100f;

    /// <summary>
    /// Twitch ID пользователя, заказавшего текущий трек
    /// </summary>
    [MaxLength(50)]
    public string? CurrentTrackRequestedBy { get; set; }

    /// <summary>
    /// Ссылка на пользователя Twitch, заказавшего текущий трек
    /// </summary>
    [ForeignKey(nameof(CurrentTrackRequestedBy))]
    public TwitchUser? CurrentTrackRequestedByTwitchUser { get; set; }

    [ForeignKey(nameof(CurrentTrackId))]
    public BaseTrackInfo? CurrentTrack { get; set; }

    [ForeignKey(nameof(NextTrackId))]
    public BaseTrackInfo? NextTrack { get; set; }
}
