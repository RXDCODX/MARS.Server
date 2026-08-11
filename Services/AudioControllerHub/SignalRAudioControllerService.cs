using System.Text.Json;
using MARS.Server.Hubs.AudioControllerHub;
using MARS.Server.Services.Obs;
using MARS.Server.Services.SoundBarService.Entitys;
using MARS.Shared.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MARS.Server.Services.AudioControllerHub;

/// <summary>
/// Unified SignalR-based service that implements both ISoundBar and IObsService.
/// Replaces HttpObsService + SoundBarHttpClient.
/// </summary>
public class SignalRAudioControllerService(
    IHubContext<Hubs.AudioControllerHub.AudioControllerHub, IAudioControllerHub> hubContext,
    AudioControllerCommandTracker tracker,
    ILogger<SignalRAudioControllerService> logger
) : ISoundBar, IObsService
{
    private const string GroupName = "audio-controllers";

    public bool IsConnected { get; private set; }

    public bool IsPaused { get; private set; }

    // ── ISoundBar ──

    public async Task Mute(params string[] args)
    {
        var processNames = args is { Length: > 0 } ? args : ["obs64", "obs32", "obs-browser-page"];
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).MuteProcesses(correlationId, processNames);
        var response = await tracker.AwaitResponseAsync(correlationId);
        if (!response.Success)
        {
            logger.LogError("Mute failed: {Error}", response.Error);
        }
    }

    public async Task Unmute()
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).UnmuteProcesses(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        if (!response.Success)
        {
            logger.LogError("Unmute failed: {Error}", response.Error);
        }
    }

    public async Task<string> GetBagCount()
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).GetBagCount(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return response.Success ? (response.Data ?? "No data") : $"Error: {response.Error}";
    }

    public async Task<bool> CheckHealthAsync()
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).Ping(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId, TimeSpan.FromSeconds(5));
        return response.Success;
    }

    // ── IObsService ──

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).ConnectObs(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        if (response.Success)
        {
            IsConnected = true;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).DisconnectObs(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        if (response.Success)
        {
            IsConnected = false;
            IsPaused = false;
        }
    }

    public async Task<string> ScreenshotAsync(
        string? sourceName = null,
        CancellationToken ct = default
    )
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).ScreenshotObs(correlationId, sourceName);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return response.Data ?? string.Empty;
    }

    public async Task<ObsPauseResult> FreezeAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).FreezeObs(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return ParsePauseResult(response);
    }

    public async Task<ObsPauseResult> UnfreezeAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).UnfreezeObs(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return ParsePauseResult(response);
    }

    public async Task<ObsPauseResult> SwitchToPauseSceneAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).SwitchToPauseScene(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return ParsePauseResult(response);
    }

    public async Task<ObsPauseResult> SwitchFromPauseSceneAsync(CancellationToken ct = default)
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).SwitchFromPauseScene(correlationId);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return ParsePauseResult(response);
    }

    public async Task<ObsPauseResult> TogglePauseAsync(
        ObsPauseMode mode = ObsPauseMode.FreezeFrame,
        CancellationToken ct = default
    )
    {
        var correlationId = tracker.CreateCommand();
        await hubContext.Clients.Group(GroupName).TogglePauseObs(correlationId, (int)mode);
        var response = await tracker.AwaitResponseAsync(correlationId);
        return ParsePauseResult(response);
    }

    // ── Helpers ──

    private ObsPauseResult ParsePauseResult(AudioControllerResponse response)
    {
        if (!response.Success)
        {
            return ObsPauseResult.Fail(response.Error ?? "Unknown error");
        }

        var dto = JsonSerializer.Deserialize<ObsPauseResultDto>(response.Data ?? "{}");
        if (dto is null)
        {
            return ObsPauseResult.Fail("Failed to deserialize response");
        }

        IsPaused = dto.IsPaused;
        return dto.Success
            ? ObsPauseResult.Ok(dto.IsPaused, dto.ScreenshotPath)
            : ObsPauseResult.Fail(dto.Error ?? "Operation failed");
    }
}
