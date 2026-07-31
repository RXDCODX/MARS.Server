using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using MARS.Server.Exstensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Enums;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;

namespace MARS.Server.Services.Twitch.Client;

public class TwitchConnectionManager : IHostedService, IAsyncDisposable
{
    private readonly ILogger<TwitchConnectionManager> _logger;
    private readonly IOptionsMonitor<TwitchConfiguration> _twitchOptions;
    private readonly ILoggerFactory _loggerFactory;

    private TwitchClient _client;
    private IClient _transport;
    private readonly ConnectionCredentials _credentials;
    private string _currentOAuth;
    private readonly IDisposable? _changeToken;
    private bool _initialized;

    public bool IsConnected => _client.IsConnected;
    private DateTime? _lastConnectedAt;
    private DateTime? _lastDisconnectedAt;
    private string? _lastDisconnectReason;

    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private int _reconnectAttempts;
    private bool _isReconnecting;
    private bool _manualDisconnect;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private const int MaxReconnectAttempts = 10;
    private const int DeepRecreationThreshold = 4;
    private const int BaseDelaySeconds = 2;
    private const int MaxDelaySeconds = 300;

    public TwitchClientProxy Proxy { get; }

    public TwitchConnectionManager(
        ILogger<TwitchConnectionManager> logger,
        IOptionsMonitor<TwitchConfiguration> twitchOptions,
        ILoggerFactory loggerFactory
    )
    {
        _logger = logger;
        _twitchOptions = twitchOptions;
        _loggerFactory = loggerFactory;

        var config = twitchOptions.CurrentValue;

        _transport = CreateTransportClient(config.TransportProtocol);
        _client = CreateTwitchClient(config.TransportProtocol);
        Proxy = new TwitchClientProxy(_client);

        _currentOAuth = config.OAuth;

        _credentials = new ConnectionCredentials(TwitchExstension.BotName, _currentOAuth);

        _changeToken = twitchOptions.OnChange(OnConfigurationChanged);

        WireEvents();
    }

    private IClient CreateTransportClient(string protocol)
    {
        var options = new ClientOptions(
            reconnectionPolicy: new NoReconnectionPolicy(),
            useSsl: true,
            disconnectWait: 1500,
            clientType: ClientType.Chat
        );

        if (string.Equals(protocol, "Tcp", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Using TCP transport for Twitch connection");
            return new TcpClient(options, _loggerFactory.CreateLogger<TcpClient>());
        }

        _logger.LogInformation("Using WebSocket transport for Twitch connection");
        return new WebSocketClient(options, _loggerFactory.CreateLogger<WebSocketClient>());
    }

    private TwitchClient CreateTwitchClient(string protocol)
    {
        var isTcp = string.Equals(protocol, "Tcp", StringComparison.OrdinalIgnoreCase);

        return new TwitchClient(
            client: _transport,
            protocol: isTcp ? ClientProtocol.TCP : ClientProtocol.WebSocket,
            sendOptions: new SendOptions(),
            loggerFactory: _loggerFactory
        );
    }

    private void WireEvents()
    {
        _client.OnConnected += (_, _) =>
        {
            _lastConnectedAt = DateTime.Now;
            _reconnectAttempts = 0;
            _isReconnecting = false;
            _logger.LogInformation("Twitch chat connected as {Bot}", TwitchExstension.BotName);
            return Task.CompletedTask;
        };

        _client.OnDisconnected += (_, _) =>
        {
            _lastDisconnectedAt = DateTime.Now;
            _logger.LogWarning("Twitch chat disconnected");

            if (!_manualDisconnect && !_isReconnecting)
            {
                _ = TryReconnectAsync();
            }

            return Task.CompletedTask;
        };

        _client.OnConnectionError += (_, args) =>
        {
            _lastDisconnectedAt = DateTime.Now;
            _lastDisconnectReason = args.Error?.Message ?? "Unknown";
            _logger.LogError("Twitch connection error: {Message}", args.Error?.Message);

            if (!_manualDisconnect && !_isReconnecting)
            {
                _ = TryReconnectAsync();
            }

            return Task.CompletedTask;
        };
    }

    private void OnConfigurationChanged(TwitchConfiguration config, string? name)
    {
        var currentProtocol = _transport is TcpClient ? "Tcp" : "WebSocket";

        if (
            !string.Equals(
                currentProtocol,
                config.TransportProtocol,
                StringComparison.OrdinalIgnoreCase
            ) && !_manualDisconnect
        )
        {
            _logger.LogWarning(
                "Twitch transport protocol change from {Old} to {New} requires service restart. Reconnect to apply.",
                currentProtocol,
                config.TransportProtocol
            );
        }

        if (
            !string.Equals(_currentOAuth, config.OAuth, StringComparison.Ordinal)
            && _client.IsConnected
            && !_manualDisconnect
        )
        {
            _logger.LogInformation("Twitch OAuth token changed, reconnecting...");
            _currentOAuth = config.OAuth;
            _ = ReconnectAsync();
        }
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
            await _reconnectCts?.CancelAsync()!;

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

    public async ValueTask DisposeAsync()
    {
        _changeToken?.Dispose();

        await StopAsync(CancellationToken.None);

        _transport?.Dispose();
        _reconnectLock.Dispose();
        _connectLock.Dispose();
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
            await _reconnectCts?.CancelAsync()!;

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

                bool success;
                if (_reconnectAttempts < DeepRecreationThreshold)
                {
                    // Simple reconnect — reuse existing client
                    await EnsureInitializedAndConnected();
                    success = _client.IsConnected;
                }
                else
                {
                    // Deep recreation — dispose old client, create new one, swap via proxy
                    _logger.LogWarning(
                        "Simple reconnect failed {N} times, performing deep client recreation",
                        _reconnectAttempts - 1
                    );
                    success = await TryDeepRecreationAsync();
                }

                if (success)
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

    private async Task<bool> TryDeepRecreationAsync()
    {
        // 1. Safely disconnect old client
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting old client during deep recreation");
        }

        // 2. Dispose old transport
        _transport?.Dispose();

        // 3. Create new transport + client
        var config = _twitchOptions.CurrentValue;
        _transport = CreateTransportClient(config.TransportProtocol);
        _client = CreateTwitchClient(config.TransportProtocol);

        // 4. Atomic swap via proxy — old client is disposed inside ReplaceClient
        Proxy.ReplaceClient(_client);

        // 5. Re-wire manager events on new client
        WireEvents();

        // 6. Reset state and connect
        _initialized = false;
        await EnsureInitializedAndConnected();

        return _client.IsConnected;
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
                    when (ex.Message.Contains(
                            "already been started",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
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
