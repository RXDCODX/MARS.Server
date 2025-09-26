namespace MARS.Server.Services.SoundRequest.Entitys;

/// <summary>
/// Represents a track requested by a user in the sound request system.
/// </summary>
public class UserRequestedTrack
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public string? TwitchDisplayName { get; set; }
    public required string TwitchId { get; set; }
    public int Order { get; set; }

    // Внешний ключ для связи
    public Guid RequestedTrackId { get; set; }
    public required BaseTrackInfo RequestedTrack { get; set; }
}
