using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MARS.Server.Services.PyroAlerts.Entitys;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Table("Alerts")]
public class MediaInfo
{
    [Key]
    [Required]
    public Guid Id { get; init; } = Guid.NewGuid();
    public required MediaTextInfo TextInfo { get; init; }
    public required MediaFileInfo FileInfo { get; init; }
    public required MediaPositionInfo PositionInfo { get; init; }
    public required MediaMetaInfo MetaInfo { get; init; }
    public required MediaStylesInfo StylesInfo { get; init; }

    public MediaInfo CloneTo()
    {
        return (MediaInfo)MemberwiseClone();
    }

    /// <summary>
    /// Конструктор копирования для использования в наследниках
    /// </summary>
    /// <param name="source">Источник данных</param>
    [SetsRequiredMembers]
    protected MediaInfo(ApiMediaInfo source)
    {
        Id = source.Id;
        TextInfo = source.TextInfo;
        FileInfo = source.FileInfo;
        PositionInfo = source.PositionInfo;
        MetaInfo = source.MetaInfo;
        StylesInfo = source.StylesInfo;
    }

    /// <summary>
    /// Конструктор копирования для использования в наследниках
    /// </summary>
    /// <param name="source">Источник данных</param>
    [SetsRequiredMembers]
    protected MediaInfo(MediaInfo source)
    {
        Id = source.Id;
        TextInfo = source.TextInfo;
        FileInfo = source.FileInfo;
        PositionInfo = source.PositionInfo;
        MetaInfo = source.MetaInfo;
        StylesInfo = source.StylesInfo;
    }

    /// <summary>
    /// Необходим для EF и сериализации
    /// </summary>
    public MediaInfo() { }
}
