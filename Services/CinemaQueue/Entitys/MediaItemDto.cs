namespace MARS.Server.Services.CinemaQueue.Entitys;

public class MediaItemDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public MediaType Type { get; set; }
    public MediaStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? AddedBy { get; set; }
    public string? TwitchUserId { get; set; }
    public string? TwitchUsername { get; set; }
    public string? Notes { get; set; }
    public bool IsNext { get; set; }
    public int EpisodeNumber { get; set; }
    public string? Season { get; set; }
    public string? Genre { get; set; }
    public string? PosterUrl { get; set; }
    public int DurationMinutes { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

public class CreateMediaItemRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public MediaType Type { get; set; }
    public int Priority { get; set; } = 0;
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? AddedBy { get; set; }
    public string? TwitchUserId { get; set; }
    public string? TwitchUsername { get; set; }
    public string? Notes { get; set; }
    public int EpisodeNumber { get; set; } = 1;
    public string? Season { get; set; }
    public string? Genre { get; set; }
    public string? PosterUrl { get; set; }
    public int DurationMinutes { get; set; } = 0;
}

public class UpdateMediaItemRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public MediaType? Type { get; set; }
    public MediaStatus? Status { get; set; }
    public int? Priority { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? Notes { get; set; }
    public int? EpisodeNumber { get; set; }
    public string? Season { get; set; }
    public string? Genre { get; set; }
    public string? PosterUrl { get; set; }
    public int? DurationMinutes { get; set; }
    public bool? IsNext { get; set; }
}
