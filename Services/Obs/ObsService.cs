using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;

namespace MARS.Server.Services.Obs;

public class ObsService(
    IOBSWebsocket obs,
    IOptions<ObsConfiguration> config,
    ILogger<ObsService> logger
) : IObsService, IHostedService, IDisposable
{
    private readonly ObsConfiguration _config = config.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _savedSceneBeforePause;
    private List<SceneItemState>? _cachedSceneItemStates;
    private bool _disposed;

    public bool IsConnected => obs.IsConnected;

    public bool IsPaused { get; private set; }

    private sealed record SceneItemState(int ItemId, string SourceName, string? GroupName, bool WasEnabled);

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
            if (obs.IsConnected)
            {
                logger.LogDebug("Already connected to OBS");
                return Task.CompletedTask;
            }

            logger.LogInformation("Connecting to OBS at {Url}", url);

            obs.ConnectAsync(url, _config.Password);

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
            if (obs.IsConnected)
            {
                obs.Disconnect();
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

        var sceneName = sourceName ?? obs.GetCurrentProgramScene();
        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDir);

        var fileName = $"obs_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.webp";
        var filePath = Path.Combine(screenshotDir, fileName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            obs.SaveSourceScreenshot(
                sceneName,
                "webp",
                filePath,
                -1,
                -1,
                _config.ScreenshotQuality
            );

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

            var currentScene = obs.GetCurrentProgramScene();
            _savedSceneBeforePause = currentScene;

            var screenshotPath = await TakeScreenshotInternalAsync(currentScene);

            ShowFreezeFrameSource(currentScene);
            HideNonAlertSourcesAndCache(currentScene);

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

            var scene = _savedSceneBeforePause ?? obs.GetCurrentProgramScene();

            HideFreezeFrameSource(scene);
            RestoreCachedSources(scene);

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

            var currentScene = obs.GetCurrentProgramScene();
            _savedSceneBeforePause = currentScene;

            var screenshotPath = await TakeScreenshotInternalAsync(currentScene);

            UpdatePauseImageSource(screenshotPath);

            obs.SetCurrentProgramScene(_config.PauseSceneName);

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

            var targetScene = _savedSceneBeforePause ?? obs.GetCurrentProgramScene();

            obs.SetCurrentProgramScene(targetScene);

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

    private Task<string> TakeScreenshotInternalAsync(string sceneName)
    {
        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDir);

        var fileName = $"obs_pause_{DateTime.Now:yyyyMMdd_HHmmss}.webp";
        var filePath = Path.Combine(screenshotDir, fileName);

        obs.SaveSourceScreenshot(sceneName, "webp", filePath, -1, -1, _config.ScreenshotQuality);

        return Task.FromResult(filePath);
    }

    private void ShowFreezeFrameSource(string sceneName)
    {
        try
        {
            var itemId = obs.GetSceneItemId(sceneName, _config.FreezeFrameSourceName, 0);
            obs.SetSceneItemEnabled(sceneName, itemId, true);
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

    private void HideFreezeFrameSource(string sceneName)
    {
        try
        {
            var itemId = obs.GetSceneItemId(sceneName, _config.FreezeFrameSourceName, 0);
            obs.SetSceneItemEnabled(sceneName, itemId, false);
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

    private List<SceneItemState> GetSceneItemsWithGroupInfo(string sceneName)
    {
        var response = obs.SendRequest(
            "GetSceneItemList",
            new JObject { { "sceneName", sceneName } }
        );

        var items = (JArray?)response["sceneItems"] ?? [];
        var result = new List<SceneItemState>(items.Count);

        foreach (var item in items)
        {
            result.Add(
                new SceneItemState(
                    item["sceneItemId"]?.Value<int>() ?? 0,
                    item["sourceName"]?.Value<string>() ?? string.Empty,
                    item["groupName"]?.Value<string>(),
                    item["sceneItemEnabled"]?.Value<bool>() ?? false
                )
            );
        }

        return result;
    }

    private void HideNonAlertSourcesAndCache(string sceneName)
    {
        try
        {
            var items = GetSceneItemsWithGroupInfo(sceneName);
            _cachedSceneItemStates = items;

            foreach (var item in items)
            {
                if (
                    item.SourceName == _config.FreezeFrameSourceName
                    || item.SourceName == _config.PauseImageSourceName
                )
                {
                    continue;
                }

                if (
                    !string.IsNullOrEmpty(item.GroupName)
                    && string.Equals(
                        item.GroupName,
                        _config.AlertsGroupName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                obs.SetSceneItemEnabled(sceneName, item.ItemId, false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to hide content sources in scene '{Scene}'", sceneName);
        }
    }

    private void RestoreCachedSources(string sceneName)
    {
        if (_cachedSceneItemStates == null)
        {
            return;
        }

        try
        {
            foreach (var item in _cachedSceneItemStates)
            {
                if (
                    item.SourceName == _config.FreezeFrameSourceName
                    || item.SourceName == _config.PauseImageSourceName
                )
                {
                    continue;
                }

                if (
                    !string.IsNullOrEmpty(item.GroupName)
                    && string.Equals(
                        item.GroupName,
                        _config.AlertsGroupName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                obs.SetSceneItemEnabled(sceneName, item.ItemId, item.WasEnabled);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to restore content sources in scene '{Scene}'", sceneName);
        }
        finally
        {
            _cachedSceneItemStates = null;
        }
    }

    private void UpdatePauseImageSource(string imagePath)
    {
        try
        {
            var settings = new Newtonsoft.Json.Linq.JObject { { "file", imagePath } };
            obs.SetInputSettings(_config.PauseImageSourceName, settings, true);
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
        if (!obs.IsConnected)
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
        obs?.Disconnect();
        _lock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
