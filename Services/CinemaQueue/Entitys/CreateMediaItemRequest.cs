namespace MARS.Server.Services.CinemaQueue.Entitys;

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
