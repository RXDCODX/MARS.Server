namespace MARS.Server.Services.SoundRequest.Entities;

/// <summary>
/// Текущее состояние аудио/видео плеера
/// </summary>
public class PlayerState
{
    /// <summary>
    /// Уникальный идентификатор состояния плеера
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// ID текущего элемента очереди (foreign key)
    /// </summary>
    public Guid? CurrentQueueItemId { get; set; }

    /// <summary>
    /// ID следующего элемента очереди (foreign key)
    /// </summary>
    public Guid? NextQueueItemId { get; set; }

    /// <summary>
    /// Текущая позиция воспроизведения трека (прогресс)
    /// </summary>
    public TimeSpan? CurrentTrackProgress { get; set; }

    /// <summary>
    /// Текущее состояние воспроизведения плеера
    /// </summary>
    public PlaybackState State { get; set; } = PlaybackState.Stopped;

    /// <summary>
    /// Режим отображения видео в плеере
    /// </summary>
    public VideoDisplay VideoState { get; set; } = VideoDisplay.Video;

    /// <summary>
    /// Звук выключен (независимо от состояния воспроизведения)
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Уровень громкости плеера (0-100)
    /// </summary>
    public float Volume { get; set; } = 100f;

    /// <summary>
    /// Ссылка на текущий элемент очереди
    /// </summary>
    [ForeignKey(nameof(CurrentQueueItemId))]
    public QueueItem? CurrentQueueItem { get; set; }

    /// <summary>
    /// Ссылка на следующий элемент очереди
    /// </summary>
    [ForeignKey(nameof(NextQueueItemId))]
    public QueueItem? NextQueueItem { get; set; }
}
