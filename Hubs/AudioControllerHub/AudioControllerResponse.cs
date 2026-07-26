namespace MARS.Server.Hubs.AudioControllerHub;

public class AudioControllerResponse
{
    public required string CorrelationId { get; set; }

    public bool Success { get; set; }

    public string? Data { get; set; }

    public string? Error { get; set; }
}

public class ObsPauseResultDto
{
    public bool Success { get; set; }

    public bool IsPaused { get; set; }

    public string? Error { get; set; }

    public string? ScreenshotPath { get; set; }
}

public class ObsStatusDto
{
    public bool IsConnected { get; set; }

    public bool IsPaused { get; set; }
}
