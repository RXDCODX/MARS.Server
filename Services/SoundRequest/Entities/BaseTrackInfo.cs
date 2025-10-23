using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.SoundRequest.Entities;

public class BaseTrackInfo
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [MaxLength(300)]
    public required string TrackName { get; set; }

    public string[]? Authors { get; set; }

    public TimeSpan Duration { get; set; }

    public required Uri Url { get; init; }

    public DateTime LastTimePlays { get; set; } = DateTime.UnixEpoch;

    public Uri? ArtworkUrl { get; set; }

    public string? VideoId { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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
