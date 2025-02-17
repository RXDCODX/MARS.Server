using System.Text.Json.Serialization;

namespace MARS.Server.Services.PyroAlerts.Entitys;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Table("Alerts")]
public class MediaInfo
{
    [Key]
    [Required]
    public Guid Id { get; private init; } = Guid.NewGuid();
    public required MediaTextInfo TextInfo { get; init; }
    public required MediaFileInfo FileInfo { get; init; }
    public required MediaPositionInfo PositionInfo { get; init; }
    public required MediaMetaInfo MetaInfo { get; init; }
    public required MediaStylesInfo StylesInfo { get; init; }

    public MediaInfo CloneTo()
    {
        return (MediaInfo)MemberwiseClone();
    }
}
