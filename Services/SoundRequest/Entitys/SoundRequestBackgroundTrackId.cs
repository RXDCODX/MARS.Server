namespace MARS.Server.Services.SoundRequest.Entitys;

/// <summary>
/// Represents the identifier for a background track in the sound request system.
/// </summary>
[Keyless]
public class SoundRequestBackgroundTrackId
{
    public Guid TrackId { get; set; }

    [ForeignKey(nameof(TrackId))]
    public required BaseTrackInfo BaseTrackInfo { get; set; }
}
