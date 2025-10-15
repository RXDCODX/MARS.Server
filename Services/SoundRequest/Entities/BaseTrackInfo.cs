using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
