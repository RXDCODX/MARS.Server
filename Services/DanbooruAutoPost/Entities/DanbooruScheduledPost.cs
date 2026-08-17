using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruScheduledPost
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConfigId { get; set; }

    public DateTime ScheduledAtUtc { get; set; }

    public ScheduledPostStatus Status { get; set; } = ScheduledPostStatus.Pending;

    public DateTime? PostedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ConfigId))]
    public DanbooruAutoPostConfig Config { get; set; } = null!;
}
