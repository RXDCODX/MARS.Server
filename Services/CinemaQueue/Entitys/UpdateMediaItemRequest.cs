using System;

namespace MARS.Server.Services.CinemaQueue.Entitys;

public class UpdateMediaItemRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? MediaUrl { get; set; }
    public MediaStatus? Status { get; set; }
    public int? Priority { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public string? Notes { get; set; }
    public bool? IsNext { get; set; }
}
