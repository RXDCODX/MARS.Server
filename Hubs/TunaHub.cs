using System.Net.WebSockets;
using System.Text.Json.Serialization;
using MARS.Server.Hubs.Models.TunaHub;
using SignalRSwaggerGen.Attributes;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/tuna")]
public class TunaHub : Hub
{
    private static ISingleClientProxy? _yandexMusicApplication;
    private static TunaMusicDTO? _lastState;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Others.SendCoreAsync("TunaMusicInfo", [_lastState]);
    }

    public Task SendPlayerData(TunaMusicDTO info)
    {
        _lastState = info;
        return Clients.Others.SendCoreAsync("TunaMusicInfo", [_lastState]);
    }

    public Task BeYm()
    {
        if (_yandexMusicApplication != null)
        {
            throw new WebSocketException();
        }

        _yandexMusicApplication = Clients.Caller;
        return Task.CompletedTask;
    }

    public Task<TunaMusicDtoRoot> GetYandexMusicTracks(string title)
    {
        ArgumentNullException.ThrowIfNull(_yandexMusicApplication);

        return _yandexMusicApplication.InvokeCoreAsync<TunaMusicDtoRoot>(
            "SearchTrack",
            [title],
            CancellationToken.None
        );
    }
}

public class TunaMusicData
{
    [JsonPropertyName("cover")]
    public required string Cover { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("artists")]
    public required string[] Artists { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("progress")]
    public ulong Progression { get; set; }

    [JsonPropertyName("duration")]
    public ulong Duration { get; set; }

    [JsonPropertyName("album_url")]
    public required string AlbumUrl { get; set; }
}

public class TunaMusicDTO
{
    [JsonPropertyName("data")]
    public required TunaMusicData Data { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Date { get; set; }
}
