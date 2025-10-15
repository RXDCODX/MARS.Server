using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.SoundRequest.Entities;

public class UserRequestedTrack
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public string? TwitchDisplayName { get; set; }

    public required string TwitchId { get; set; }

    public int Order { get; set; }

    public Guid? RequestedTrackId { get; set; }

    public required BaseTrackInfo RequestedTrack { get; set; }
}
