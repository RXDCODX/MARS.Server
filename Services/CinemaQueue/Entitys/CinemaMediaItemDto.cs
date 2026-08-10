namespace MARS.Server.Services.CinemaQueue.Entitys;

public class CinemaMediaItemDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public required string MediaUrl { get; set; }
    public MediaStatus Status { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public string? AddedBy { get; set; }
    public string? TwitchUserId { get; set; }
    public string? TwitchUsername { get; set; }
    public string? Notes { get; set; }
    public bool IsNext { get; set; }
    public DateTime? LastModified { get; set; }
}
