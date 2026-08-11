using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.SoundRequest.Entities;

/// <summary>
/// Базовая информация о треке (песня, видео)
/// </summary>
public class BaseTrackInfo
{
    /// <summary>
    /// Уникальный идентификатор трека
    /// </summary>
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Название трека (максимум 300 символов)
    /// </summary>
    [MaxLength(300)]
    public required string TrackName { get; set; }

    /// <summary>
    /// Массив авторов/исполнителей трека
    /// </summary>
    public string[]? Authors { get; set; }

    /// <summary>
    /// Длительность трека
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// URL трека (уникальный идентификатор источника)
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// Дата и время последнего воспроизведения трека
    /// </summary>
    public DateTime LastTimePlays { get; set; } = DateTime.UnixEpoch;

    /// <summary>
    /// URL обложки/превью трека
    /// </summary>
    public Uri? ArtworkUrl { get; set; }

    /// <summary>
    /// ID видео на платформе (например, YouTube video ID)
    /// </summary>
    public string? VideoId { get; set; }

    /// <summary>
    /// Помечен как удаленный (мягкое удаление)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Дата и время создания записи в базе данных
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Дата и время последнего обновления записи
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Полное название трека в формате "Автор - Название" (вычисляемое поле, не хранится в БД)
    /// </summary>
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
