namespace MARS.Server.Services.SoundRequest.Entitys;

[Keyless]
public class SoundRequestBackgroundTrackId
{
    public Guid TrackId { get; set; }

    [ForeignKey(nameof(TrackId))]
    public required BaseTrackInfo BaseTrackInfo { get; set; }
}
