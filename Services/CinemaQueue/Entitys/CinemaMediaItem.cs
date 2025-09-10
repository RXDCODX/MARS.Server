namespace MARS.Server.Services.CinemaQueue.Entitys;

[Table("CinemaQueue")]
public class CinemaMediaItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string MediaUrl { get; set; }

    [Required]
    public MediaStatus Status { get; set; } = MediaStatus.Pending;

    [Required]
    public int Priority { get; set; } = 0;

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? ScheduledFor { get; set; }

    [MaxLength(100)]
    public string? AddedBy { get; set; }

    [MaxLength(50)]
    public string? TwitchUserId { get; set; }

    [MaxLength(100)]
    public string? TwitchUsername { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsNext { get; set; } = false;

    public DateTimeOffset? LastModified { get; set; } = DateTimeOffset.Now;
}

public enum MediaStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled,
    Postponed,
}
