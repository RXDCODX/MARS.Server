namespace MARS.Server.Services.PyroAlerts.Entitys;

public struct MediaDto(MediaInfo mediaInfo)
{
    [Required]
    public required MediaInfo MediaInfo { get; init; } = mediaInfo;

    public DateTime UploadStartTime { get; set; } = DateTime.Now;
}
