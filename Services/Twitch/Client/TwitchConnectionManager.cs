using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace MARS.Server.Services.Twitch.Client;

public class TwitchConnectionManager : IHostedService
{
    private readonly ILogger<TwitchConnectionManager> _logger;

    private readonly WebSocketClient _webSocketClient;
    private readonly TwitchClient _client;
    private readonly ConnectionCredentials _credentials;
    private bool _initialized;

    private bool IsConnected => _webSocketClient.IsConnected;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private string? _lastDisconnectReason;

    public ITwitchClient Client => _client;

    public TwitchConnectionManager(
        ILogger<TwitchConnectionManager> logger,
        IOptions<TwitchConfiguration> twitchOptions,
        ILoggerFactory loggerFactory
    )
    {
        _logger = logger;

        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30),
            DisconnectWait = (int)TimeSpan.FromSeconds(2).TotalMilliseconds,
        };

        _webSocketClient = new WebSocketClient(clientOptions);
        _client = new TwitchClient(
            _webSocketClient,
            ClientProtocol.WebSocket,
            loggerFactory.CreateLogger<TwitchClient>()
        );

        _credentials = new ConnectionCredentials(
            TwitchExstension.BotName,
            twitchOptions.Value.OAuth
        );

        _client.OnConnected += (_, _) =>
        {
            _lastConnectedAt = DateTimeOffset.Now;
            _logger.LogInformation("Twitch chat connected as {Bot}", TwitchExstension.BotName);
        };

        _client.OnDisconnected += (_, _) =>
        {
            _lastDisconnectedAt = DateTimeOffset.Now;
            _logger.LogWarning("Twitch chat disconnected");
        };

        _client.OnConnectionError += (_, args) =>
        {
            _lastDisconnectedAt = DateTimeOffset.Now;
            _lastDisconnectReason = args.Error?.Message ?? "Unknown";
            _logger.LogError("Twitch connection error: {Message}", args.Error?.Message);
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            EnsureInitializedAndConnected();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Twitch client on startup");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping Twitch client");
        }

        return Task.CompletedTask;
    }

    public string GetStatus()
    {
        var joined = string.Join(", ", _client.JoinedChannels.Select(c => c.Channel));
        return $"Connected: {IsConnected}\n"
            + $"JoinedChannels: {(string.IsNullOrWhiteSpace(joined) ? "-" : joined)}\n"
            + $"LastConnectedAt: {_lastConnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}\n"
            + $"LastDisconnectedAt: {_lastDisconnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}\n"
            + $"LastDisconnectReason: {_lastDisconnectReason ?? "-"}";
    }

    public Task<bool> ReconnectAsync()
    {
        try
        {
            _logger.LogInformation("Manual Twitch reconnect requested");

            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            // Small delay to let the socket close cleanly
            Thread.Sleep(TimeSpan.FromMilliseconds(250));

            EnsureInitializedAndConnected();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect Twitch client");
            return Task.FromResult(false);
        }
    }

    private void EnsureInitializedAndConnected()
    {
        if (!_initialized)
        {
            _client.Initialize(_credentials, TwitchExstension.Channel);
            _initialized = true;
        }

        if (!_client.IsConnected)
        {
            _client.Connect();
        }
    }
}
