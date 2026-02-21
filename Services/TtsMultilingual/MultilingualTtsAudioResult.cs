namespace MARS.Server.Services.TtsMultilingual;

public class MultilingualTtsAudioResult
{
    public byte[] AudioBytes { get; set; } = [];
    public string ContentType { get; set; } = "audio/wav";
}