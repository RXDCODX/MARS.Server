using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.SoundRequest.Entities;

public class PlayerState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public BaseTrackInfo? CurrentTrack { get; set; }

    public BaseTrackInfo? NextTrack { get; set; }

    public TimeSpan? CurrentTrackDuration { get; set; }

    public bool IsPaused { get; set; }

    public bool IsMuted { get; set; }

    public bool IsStoped { get; set; }

    public int Volume { get; set; } = 100;
}
