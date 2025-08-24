using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.CinemaQueue.Entitys;

[Table("CinemaQueue")]
public class MediaItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public MediaType Type { get; set; }

    [Required]
    public MediaStatus Status { get; set; } = MediaStatus.Pending;

    [Required]
    public int Priority { get; set; } = 0;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    [Required]
    public DateTimeOffset? ScheduledFor { get; set; }

    [MaxLength(100)]
    public string? AddedBy { get; set; }

    [MaxLength(50)]
    public string? TwitchUserId { get; set; }

    [MaxLength(100)]
    public string? TwitchUsername { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public bool IsNext { get; set; } = false;

    [Required]
    public int EpisodeNumber { get; set; } = 1;

    [MaxLength(100)]
    public string? Season { get; set; }

    [MaxLength(100)]
    public string? Genre { get; set; }

    [MaxLength(200)]
    public string? PosterUrl { get; set; }

    [Required]
    public int DurationMinutes { get; set; } = 0;

    [Required]
    public DateTimeOffset? LastModified { get; set; } = DateTimeOffset.Now;
}

public enum MediaType
{
    Movie,
    Series,
    Anime,
    Documentary,
    Special
}

public enum MediaStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled,
    Postponed
}
