namespace MARS.Server.Services.StreamAcrhive.Models;

public class VideoInfo
{
    public TimeSpan Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Codec { get; set; }
    public long Bitrate { get; set; }
    public double FrameRate { get; set; }
}
