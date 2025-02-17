namespace MARS.Server.Services.PyroAlerts.Entitys;

public struct MediaDto
{
    public MediaDto(MediaInfo mediaInfo)
    {
        MediaInfo = mediaInfo;
    }

    [Required]
    public required MediaInfo MediaInfo { get; init; }

    public DateTime UploadStartTime { get; set; } = DateTime.Now;
}
