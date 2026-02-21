namespace MARS.Server.Controllers;

public class MultilingualTtsSynthesizeRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Speaker { get; set; } = string.Empty;
}