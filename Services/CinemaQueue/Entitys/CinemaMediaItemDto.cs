namespace MARS.Server.Services.CinemaQueue.Entitys;

public class CinemaMediaItemDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string MediaUrl { get; set; }
    public MediaStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? AddedBy { get; set; }
    public string? TwitchUserId { get; set; }
    public string? TwitchUsername { get; set; }
    public string? Notes { get; set; }
    public bool IsNext { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}

public class CreateMediaItemRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string MediaUrl { get; set; }
    public int Priority { get; set; } = 0;
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? AddedBy { get; set; }
    public string? TwitchUserId { get; set; }
    public string? TwitchUsername { get; set; }
    public string? Notes { get; set; }
}

public class UpdateMediaItemRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? MediaUrl { get; set; }
    public MediaStatus? Status { get; set; }
    public int? Priority { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public string? Notes { get; set; }
    public bool? IsNext { get; set; }
}
