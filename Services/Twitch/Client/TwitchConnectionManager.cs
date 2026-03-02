using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.Client;

public class TwitchConnectionManager : IHostedService
{
    private readonly ILogger<TwitchConnectionManager> _logger;

    private readonly TwitchClient _client;
    private readonly ConnectionCredentials _credentials;
    private bool _initialized;

    private bool IsConnected => _client.IsConnected;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private string? _lastDisconnectReason;

    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private int _reconnectAttempts;
    private bool _isReconnecting;
    private bool _manualDisconnect;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private const int MaxReconnectAttempts = 10;
    private const int BaseDelaySeconds = 2;
    private const int MaxDelaySeconds = 300;

    public ITwitchClient Client => _client;

    public TwitchConnectionManager(
        ILogger<TwitchConnectionManager> logger,
        IOptions<TwitchConfiguration> twitchOptions,
        ILoggerFactory loggerFactory
    )
    {
        _logger = logger;

        _client = new TwitchClient(loggerFactory: loggerFactory);

        _credentials = new ConnectionCredentials(
            TwitchExstension.BotName,
            twitchOptions.Value.OAuth
        );

        _client.OnConnected += (_, _) =>
        {
            _lastConnectedAt = DateTimeOffset.Now;
            _reconnectAttempts = 0;
            _isReconnecting = false;
            _logger.LogInformation("Twitch chat connected as {Bot}", TwitchExstension.BotName);
            return Task.CompletedTask;
        };

        _client.OnDisconnected += (_, _) =>
        {
            _lastDisconnectedAt = DateTimeOffset.Now;
            _logger.LogWarning("Twitch chat disconnected");

            if (!_manualDisconnect && !_isReconnecting)
            {
                _ = TryReconnectAsync();
            }

            return Task.CompletedTask;
        };

        _client.OnConnectionError += (_, args) =>
        {
            _lastDisconnectedAt = DateTimeOffset.Now;
            _lastDisconnectReason = args.Error?.Message ?? "Unknown";
            _logger.LogError("Twitch connection error: {Message}", args.Error?.Message);

            if (!_manualDisconnect && !_isReconnecting)
            {
                _ = TryReconnectAsync();
            }

            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _reconnectCts = new CancellationTokenSource();
            _manualDisconnect = false;
            await EnsureInitializedAndConnected();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Twitch client on startup");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manualDisconnect = true;
            _reconnectCts?.Cancel();

            if (_reconnectTask != null)
            {
                await _reconnectTask;
            }

            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }

            _reconnectCts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping Twitch client");
        }
    }

    public string GetStatus()
    {
        var joined = string.Join(", ", _client.JoinedChannels.Select(c => c.Channel));
        return $"Connected: {IsConnected}\n"
            + $"JoinedChannels: {(string.IsNullOrWhiteSpace(joined) ? "-" : joined)}\n"
            + $"LastConnectedAt: {_lastConnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}\n"
            + $"LastDisconnectedAt: {_lastDisconnectedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"}\n"
            + $"LastDisconnectReason: {_lastDisconnectReason ?? "-"}\n"
            + $"ReconnectAttempts: {_reconnectAttempts}\n"
            + $"IsReconnecting: {_isReconnecting}";
    }

    public async Task<bool> ReconnectAsync()
    {
        try
        {
            _logger.LogInformation("Manual Twitch reconnect requested");

            _manualDisconnect = true;
            _reconnectCts?.Cancel();

            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));

            _reconnectAttempts = 0;
            _manualDisconnect = false;
            _reconnectCts = new CancellationTokenSource();

            await EnsureInitializedAndConnected();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect Twitch client");
            return false;
        }
    }

    private async Task TryReconnectAsync()
    {
        await _reconnectLock.WaitAsync();
        try
        {
            if (
                _isReconnecting
                || _manualDisconnect
                || _reconnectCts?.Token.IsCancellationRequested == true
            )
            {
                return;
            }

            _isReconnecting = true;
            _reconnectTask = ReconnectLoopAsync(_reconnectCts!.Token);
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        while (
            !cancellationToken.IsCancellationRequested && _reconnectAttempts < MaxReconnectAttempts
        )
        {
            _reconnectAttempts++;

            var delay = CalculateReconnectDelay(_reconnectAttempts);
            _logger.LogInformation(
                "Attempting to reconnect to Twitch (attempt {Attempt}/{Max}) in {Delay} seconds...",
                _reconnectAttempts,
                MaxReconnectAttempts,
                delay.TotalSeconds
            );

            try
            {
                await Task.Delay(delay, cancellationToken);

                if (_client.IsConnected)
                {
                    _logger.LogInformation("Already connected, stopping reconnect attempts");
                    _isReconnecting = false;
                    return;
                }

                await EnsureInitializedAndConnected();

                if (_client.IsConnected)
                {
                    _logger.LogInformation("Successfully reconnected to Twitch");
                    _isReconnecting = false;
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Reconnect cancelled");
                _isReconnecting = false;
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconnect attempt {Attempt} failed", _reconnectAttempts);
            }
        }

        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            _logger.LogError(
                "Failed to reconnect to Twitch after {MaxAttempts} attempts. Manual intervention required.",
                MaxReconnectAttempts
            );
        }

        _isReconnecting = false;
    }

    private static TimeSpan CalculateReconnectDelay(int attempt)
    {
        var delaySeconds = Math.Min(BaseDelaySeconds * Math.Pow(2, attempt - 1), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private async Task EnsureInitializedAndConnected()
    {
        await _connectLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                _client.Initialize(_credentials, TwitchExstension.Channel);
                _initialized = true;
            }

            if (!_client.IsConnected)
            {
                try
                {
                    await _client.ConnectAsync();
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("already been started", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        ex,
                        "Skipped duplicate Twitch connect attempt while WebSocket is already starting"
                    );
                }
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
