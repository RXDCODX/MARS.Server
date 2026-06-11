using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace MARS.Server.Services.Obs;

public class ObsService(IOptions<ObsConfiguration> config, ILogger<ObsService> logger)
    : IObsService,
        IHostedService,
        IDisposable
{
    private readonly ObsConfiguration _config = config.Value;
    private readonly OBSWebsocket _obs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _savedSceneBeforePause;
    private bool _disposed;

    public bool IsConnected => _obs.IsConnected;

    public bool IsPaused { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to auto-connect to OBS on startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisconnectAsync();
        return Task.CompletedTask;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var url = $"ws://{_config.Host}:{_config.Port}";

        try
        {
            if (_obs.IsConnected)
            {
                logger.LogDebug("Already connected to OBS");
                return Task.CompletedTask;
            }

            logger.LogInformation("Connecting to OBS at {Url}", url);

            _obs.ConnectAsync(url, _config.Password);

            logger.LogInformation("Connected to OBS successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to OBS at {Url}", url);
            throw;
        }

        return Task.CompletedTask;
    }

    public void DisconnectAsync()
    {
        try
        {
            if (_obs.IsConnected)
            {
                _obs.Disconnect();
                logger.LogInformation("Disconnected from OBS");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during OBS disconnect");
        }
    }

    public async Task<string> ScreenshotAsync(
        string? sourceName = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureConnected();

        var sceneName = sourceName ?? _obs.GetCurrentProgramScene();
        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDir);

        var fileName = $"obs_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.webp";
        var filePath = Path.Combine(screenshotDir, fileName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _obs.SaveSourceScreenshot(sceneName, "webp", filePath, _config.ScreenshotQuality);

            logger.LogDebug("Screenshot saved to {Path}", filePath);

            return filePath;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ObsPauseResult> FreezeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();

            if (IsPaused)
            {
                return ObsPauseResult.Fail("Already paused");
            }

            var currentScene = _obs.GetCurrentProgramScene();
            _savedSceneBeforePause = currentScene;

            var screenshotPath = await TakeScreenshotInternalAsync(currentScene, cancellationToken);

            ShowFreezeFrameSource(currentScene, true);
            HideAllContentSourcesExcept(currentScene, _config.FreezeFrameSourceName, true);

            IsPaused = true;
            logger.LogInformation("Freeze frame activated on scene {Scene}", currentScene);

            return ObsPauseResult.Ok(true, screenshotPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to activate freeze frame");
            return ObsPauseResult.Fail(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ObsPauseResult> UnfreezeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();

            if (!IsPaused)
            {
                return ObsPauseResult.Fail("Not paused");
            }

            var scene = _savedSceneBeforePause ?? _obs.GetCurrentProgramScene();

            HideFreezeFrameSource(scene, true);
            ShowAllContentSourcesExcept(scene, _config.FreezeFrameSourceName, true);

            IsPaused = false;
            _savedSceneBeforePause = null;

            logger.LogInformation("Freeze frame deactivated");

            return ObsPauseResult.Ok(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deactivate freeze frame");
            return ObsPauseResult.Fail(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ObsPauseResult> SwitchToPauseSceneAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();

            if (IsPaused)
            {
                return ObsPauseResult.Fail("Already paused");
            }

            var currentScene = _obs.GetCurrentProgramScene();
            _savedSceneBeforePause = currentScene;

            var screenshotPath = await TakeScreenshotInternalAsync(currentScene, cancellationToken);

            UpdatePauseImageSource(screenshotPath);

            _obs.SetCurrentProgramScene(_config.PauseSceneName);

            IsPaused = true;
            logger.LogInformation("Switched to pause scene (from {Scene})", currentScene);

            return ObsPauseResult.Ok(true, screenshotPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch to pause scene");
            return ObsPauseResult.Fail(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ObsPauseResult> SwitchFromPauseSceneAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();

            if (!IsPaused)
            {
                return ObsPauseResult.Fail("Not paused");
            }

            var targetScene = _savedSceneBeforePause ?? _obs.GetCurrentProgramScene();

            _obs.SetCurrentProgramScene(targetScene);

            IsPaused = false;
            _savedSceneBeforePause = null;

            logger.LogInformation("Switched from pause scene to {Scene}", targetScene);

            return ObsPauseResult.Ok(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch from pause scene");
            return ObsPauseResult.Fail(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ObsPauseResult> TogglePauseAsync(
        ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken cancellationToken = default
    )
    {
        return mode switch
        {
            ObsPauseMode.FreezeFrame => IsPaused
                ? await UnfreezeAsync(cancellationToken)
                : await FreezeAsync(cancellationToken),
            ObsPauseMode.PauseScene => IsPaused
                ? await SwitchFromPauseSceneAsync(cancellationToken)
                : await SwitchToPauseSceneAsync(cancellationToken),
            _ => ObsPauseResult.Fail($"Unknown pause mode: {mode}"),
        };
    }

    private Task<string> TakeScreenshotInternalAsync(
        string sceneName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDir);

            var fileName = $"obs_pause_{DateTime.Now:yyyyMMdd_HHmmss}.webp";
            var filePath = Path.Combine(screenshotDir, fileName);

            _obs.SaveSourceScreenshot(sceneName, "webp", filePath, _config.ScreenshotQuality);

            return Task.FromResult(filePath);
        }
        catch (Exception exception)
        {
            return Task.FromException<string>(exception);
        }
    }

    private void ShowFreezeFrameSource(string sceneName, bool sceneItemEnabled)
    {
        try
        {
            var itemId = _obs.GetSceneItemId(sceneName, _config.FreezeFrameSourceName, 0);
            _obs.SetSceneItemEnabled(sceneName, itemId, sceneItemEnabled);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Freeze frame source '{Source}' not found in scene '{Scene}'",
                _config.FreezeFrameSourceName,
                sceneName
            );
        }
    }

    private void HideFreezeFrameSource(string sceneName, bool sceneItemDisabled)
    {
        try
        {
            var itemId = _obs.GetSceneItemId(sceneName, _config.FreezeFrameSourceName, 0);
            _obs.SetSceneItemEnabled(sceneName, itemId, !sceneItemDisabled);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Freeze frame source '{Source}' not found in scene '{Scene}'",
                _config.FreezeFrameSourceName,
                sceneName
            );
        }
    }

    private void HideAllContentSourcesExcept(string sceneName, string excludedSourceName, bool hide)
    {
        try
        {
            var items = _obs.GetSceneItemList(sceneName);
            foreach (var item in items)
            {
                if (
                    item.SourceName == excludedSourceName
                    || item.SourceName == _config.PauseImageSourceName
                )
                {
                    continue;
                }

                _obs.SetSceneItemEnabled(sceneName, item.ItemId, !hide);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to hide content sources in scene '{Scene}'", sceneName);
        }
    }

    private void ShowAllContentSourcesExcept(string sceneName, string excludedSourceName, bool show)
    {
        try
        {
            var items = _obs.GetSceneItemList(sceneName);
            foreach (var item in items)
            {
                if (
                    item.SourceName == excludedSourceName
                    || item.SourceName == _config.PauseImageSourceName
                )
                {
                    continue;
                }

                _obs.SetSceneItemEnabled(sceneName, item.ItemId, show);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to show content sources in scene '{Scene}'", sceneName);
        }
    }

    private void UpdatePauseImageSource(string imagePath)
    {
        try
        {
            var settings = new Newtonsoft.Json.Linq.JObject { { "file", imagePath } };
            _obs.SetInputSettings(_config.PauseImageSourceName, settings, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to update pause image source '{Source}'",
                _config.PauseImageSourceName
            );
        }
    }

    private void EnsureConnected()
    {
        if (!_obs.IsConnected)
        {
            throw new InvalidOperationException("Not connected to OBS");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _obs?.Disconnect();
        _lock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
