using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.Obs;

public interface IObsService
{
    bool IsConnected { get; }

    bool IsPaused { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<string> ScreenshotAsync(
        string? sourceName = null,
        CancellationToken cancellationToken = default
    );

    Task<ObsPauseResult> FreezeAsync(CancellationToken cancellationToken = default);

    Task<ObsPauseResult> UnfreezeAsync(CancellationToken cancellationToken = default);

    Task<ObsPauseResult> SwitchToPauseSceneAsync(CancellationToken cancellationToken = default);

    Task<ObsPauseResult> SwitchFromPauseSceneAsync(CancellationToken cancellationToken = default);

    Task<ObsPauseResult> TogglePauseAsync(
        ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken cancellationToken = default
    );
}

public enum ObsPauseMode
{
    FreezeFrame = 0,
    PauseScene = 1,
}

public class ObsPauseResult
{
    public bool Success { get; set; }

    public bool IsPaused { get; set; }

    public string? Error { get; set; }

    public string? ScreenshotPath { get; set; }

    public static ObsPauseResult Ok(bool isPaused, string? screenshotPath = null) =>
        new()
        {
            Success = true,
            IsPaused = isPaused,
            ScreenshotPath = screenshotPath,
        };

    public static ObsPauseResult Fail(string error) => new() { Success = false, Error = error };
}
