global using WTelegramClient = WTelegram.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.ApplicationState;
using MARS.Server.Configuration;
using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Telegram.BotService.Entities;
using MARS.Server.Services.Telegram.WTelegram.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TL;
using WTelegram;
using Update = Telegram.Bot.Types.Update;

namespace MARS.Server.Services.Telegram.WTelegram;

/// <summary>
/// Сервис-обертка для WTelegramClient с автоматической переавторизацией
/// </summary>
public class WTelegramClientService : IDisposable
{
    private readonly ILogger<WTelegramClientService> _logger;
    private readonly WTelegramClientConfiguration _configuration;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private WTelegramClient? _client;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _isDisposed;

    private readonly ITelegramBotClient _botClient;
    private readonly ManualResetEventSlim _codeWaitHandle = new(false);
    private string? _pendingVerificationCode;
    private volatile bool _awaitingCode;
    private int _proxyConnectivityNotificationSent;

    private sealed class ProxyConfigurationInfo
    {
        public bool IsProxyConfigured { get; init; }
        public string Description { get; init; } = "Без прокси";
    }

    public WTelegramClientService(
        ILogger<WTelegramClientService> logger,
        IOptions<WTelegramClientConfiguration> configuration,
        ITelegramBotClient botClient,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _logger = logger;
        _configuration = configuration.Value;
        _botClient = botClient;
        _dbContextFactory = dbContextFactory;

        Helpers.Log = (level, message) => _logger.Log((LogLevel)level, "{Message}", message);
    }

    /// <summary>
    /// Обрабатывает обновления от Telegram Bot для получения экземпляра ITelegramBotClient
    /// </summary>
    public Task HandleUpdate(ITelegramBotClient _, Update? update)
    {
        _logger.LogInformation("WTelegramClientService получил экземпляр ITelegramBotClient");

        if (_awaitingCode && update?.Message?.Text is { } text)
        {
            _pendingVerificationCode = text;
            _codeWaitHandle.Set();
            _logger.LogInformation("Получен код верификации через бота");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Получает статус авторизации WTelegram клиента
    /// </summary>
    public async Task<WTelegramClientStatus> GetClientStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_client?.User != null)
            {
                return new WTelegramClientStatus
                {
                    IsAuthenticated = true,
                    UserId = _client.User.id,
                    Username = _client.User.username,
                    Phone = _client.User.phone,
                };
            }

            var client = await GetClientAsync(cancellationToken);

            return new WTelegramClientStatus
            {
                IsAuthenticated = client.User != null,
                UserId = client.User?.id,
                Username = client.User?.username,
                Phone = client.User?.phone,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса WTelegram");
            return new WTelegramClientStatus { IsAuthenticated = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Получает клиент WTelegram с автоматической переавторизацией при необходимости
    /// </summary>
    public async Task<WTelegramClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        if (_client?.User != null)
        {
            return _client;
        }

        await _loginLock.WaitAsync(cancellationToken);
        try
        {
            if (_client?.User != null)
            {
                return _client;
            }

            await InitializeClientAsync(cancellationToken);
            return _client!;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>
    /// Принудительно выполняет повторную авторизацию
    /// </summary>
    public async Task ReLoginAsync(CancellationToken cancellationToken = default)
    {
        await _loginLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Начало процесса повторной авторизации WTelegram...");

            await NotifyAuthRequiredAsync("Ручная переавторизация");

            // Удаляем старую сессию из БД
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken
                );
                var session = await dbContext.WTelegramSessions.FindAsync(
                    WTelegramSession.DefaultSessionName
                );
                if (session is not null)
                {
                    dbContext.WTelegramSessions.Remove(session);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                _logger.LogInformation("Старая сессия удалена из БД");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось удалить старую сессию из БД");
            }

            // Dispose старого клиента
            if (_client != null)
            {
                await _client.DisposeAsync();
            }
            _client = null;

            // Создаем нового клиента и авторизуемся
            await InitializeClientAsync(cancellationToken);

            _logger.LogInformation("Повторная авторизация WTelegram завершена успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при повторной авторизации WTelegram");

            if (!IsConnectivityException(ex))
            {
                await NotifyAuthFailedAsync(ex.Message);
            }
            else
            {
                _logger.LogInformation(
                    "Уведомление о сетевой ошибке авторизации пропущено: диагностика прокси отправляется единично"
                );
            }

            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task InitializeClientAsync(CancellationToken cancellationToken)
    {
        _client = CreateClient();

        var proxyInfo = await ApplyProxyConfigurationAsync(cancellationToken);

        try
        {
            await PerformAuthorizationAsync(cancellationToken);
        }
        catch (Exception ex) when (proxyInfo.IsProxyConfigured && IsConnectivityException(ex))
        {
            _logger.LogWarning(
                ex,
                "Ошибка подключения к Telegram через прокси ({ProxyDescription}). Пробуем авторизацию без прокси",
                proxyInfo.Description
            );

            var proxyErrorMessage = ex.Message;
            var worksWithoutProxy = false;
            string? noProxyErrorMessage = null;

            try
            {
                if (_client != null)
                {
                    await _client.DisposeAsync();
                }

                _client = CreateClient();
                await PerformAuthorizationAsync(cancellationToken);
                worksWithoutProxy = true;
            }
            catch (Exception fallbackEx)
            {
                noProxyErrorMessage = fallbackEx.Message;
                throw;
            }
            finally
            {
                await NotifyProxyConnectivityCheckOnceAsync(
                    proxyInfo.Description,
                    proxyErrorMessage,
                    worksWithoutProxy,
                    noProxyErrorMessage
                );
            }
        }
    }

    private WTelegramClient CreateClient()
    {
        var sessionStore = new WTelegramDbSessionStore(
            _dbContextFactory,
            WTelegramSession.DefaultSessionName,
            _logger
        );

        return new WTelegramClient(key => GetConfigValue(key)!, sessionStore);
    }

    private string? GetConfigValue(string key)
    {
        return key switch
        {
            "api_id" => _configuration.AppId.ToString(),
            "api_hash" => _configuration.ApiHash,
            "phone_number" => _configuration.PhoneNumber,
            "name" => "PYROKXNEZXZ",
            "first_name" => _configuration.FirstNameLastName?.Split(' ')[0],
            "last_name" => _configuration.FirstNameLastName?.Split(' ', 2).Length > 1
                ? _configuration.FirstNameLastName.Split(' ', 2)[1]
                : null,
            "verification_code" => WaitForVerificationCode(),
            "password" => _configuration.Password,
            _ => null,
        };
    }

    private string? WaitForVerificationCode()
    {
        _codeWaitHandle.Reset();
        _pendingVerificationCode = null;
        _awaitingCode = true;

        _logger.LogWarning("Требуется код верификации. Ожидание ввода через бота...");

        NotifyVerificationCodeRequiredAsync().GetAwaiter().GetResult();

        _botClient
            ?.SendMessage(TelegramExstension.Rxdcodx, "Введите код верификации:")
            .GetAwaiter()
            .GetResult();

        _codeWaitHandle.Wait();
        _awaitingCode = false;

        var code = _pendingVerificationCode;
        _pendingVerificationCode = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Код верификации не был предоставлен");
        }

        _logger.LogInformation("Код верификации получен");
        return code;
    }

    private async Task PerformAuthorizationAsync(CancellationToken cancellationToken)
    {
        var client =
            _client ?? throw new InvalidOperationException("WTelegram клиент не инициализирован");

        var user = await client.LoginUserIfNeeded();

        _logger.LogInformation("Авторизация WTelegram успешна. Пользователь: {User}", user);

        var username = user?.username ?? user?.phone ?? "Unknown";
        await NotifyAuthSuccessAsync(username);
        Interlocked.Exchange(ref _proxyConnectivityNotificationSent, 0);
    }

    private static bool IsCodeExpiredError(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (
                current is System.Net.Http.HttpRequestException httpEx
                && httpEx.Message.Contains("PHONE_CODE_EXPIRED", StringComparison.Ordinal)
            )
            {
                return true;
            }

            if (
                current.Message.Contains("PHONE_CODE_EXPIRED", StringComparison.Ordinal)
                || current.Message.Contains("PHONE_CODE_INVALID", StringComparison.Ordinal)
            )
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private async Task<ProxyConfigurationInfo> ApplyProxyConfigurationAsync(
        CancellationToken cancellationToken
    )
    {
        var proxyValue = await GetProxyValueFromRootStateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(proxyValue) || _client is null)
        {
            return new ProxyConfigurationInfo();
        }

        if (TryParseTelegramProxyLink(proxyValue, out var mtProxyUrlFromTelegramLink))
        {
            _client.MTProxyUrl = mtProxyUrlFromTelegramLink;
            _logger.LogInformation(
                "Для WTelegram включен MTProxy из Telegram-ссылки: {RootStateKey}",
                RootStateKeys.WTelegramMtProxyUrl
            );

            return new ProxyConfigurationInfo
            {
                IsProxyConfigured = true,
                Description = "MTProxy (t.me/proxy)",
            };
        }

        if (TryParseTelegramSocksLink(proxyValue, out var socksProxyUriFromTelegramLink))
        {
            _client.TcpHandler = (destinationAddress, destinationPort) =>
                CreateTcpClientThroughProxyAsync(
                    socksProxyUriFromTelegramLink,
                    destinationAddress,
                    destinationPort
                );

            _logger.LogInformation(
                "Для WTelegram включен SOCKS5 прокси из Telegram-ссылки: {RootStateKey}",
                RootStateKeys.WTelegramProxyUrl
            );

            return new ProxyConfigurationInfo
            {
                IsProxyConfigured = true,
                Description = "SOCKS5 (t.me/socks)",
            };
        }

        if (IsMtProxyUrl(proxyValue))
        {
            _client.MTProxyUrl = proxyValue;
            _logger.LogInformation(
                "Для WTelegram включен MTProxy из RootState: {RootStateKey}",
                RootStateKeys.WTelegramMtProxyUrl
            );

            return new ProxyConfigurationInfo { IsProxyConfigured = true, Description = "MTProxy" };
        }
        else if (TryParseProxyUri(proxyValue, out var proxyUri))
        {
            _client.TcpHandler = (destinationAddress, destinationPort) =>
                CreateTcpClientThroughProxyAsync(proxyUri, destinationAddress, destinationPort);

            _logger.LogInformation(
                "Для WTelegram включен прокси {ProxyScheme} из RootState: {RootStateKey}",
                proxyUri.Scheme,
                RootStateKeys.WTelegramProxyUrl
            );

            return new ProxyConfigurationInfo
            {
                IsProxyConfigured = true,
                Description = $"{proxyUri.Scheme.ToUpperInvariant()} proxy",
            };
        }
        else
        {
            _logger.LogWarning(
                "Некорректный формат прокси для WTelegram в RootState. Ожидается MTProxy URL, socks5:// или http://"
            );

            return new ProxyConfigurationInfo();
        }
    }

    private static bool IsMtProxyUrl(string proxyValue)
    {
        return proxyValue.Contains("/proxy?", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseProxyUri(string proxyValue, out Uri proxyUri)
    {
        var isValidUri = Uri.TryCreate(proxyValue, UriKind.Absolute, out var parsedUri);
        if (
            isValidUri
            && parsedUri is not null
            && (
                parsedUri.Scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase)
                || parsedUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            proxyUri = parsedUri;
            return true;
        }

        proxyUri = null!;
        return false;
    }

    private static bool TryParseTelegramProxyLink(string proxyValue, out string mtProxyUrl)
    {
        mtProxyUrl = string.Empty;

        if (!Uri.TryCreate(proxyValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.AbsolutePath.Trim('/').Equals("proxy", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQueryString(uri.Query);
        if (
            !query.TryGetValue("server", out var server)
            || !query.TryGetValue("port", out var portRaw)
            || !query.TryGetValue("secret", out var secret)
            || string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(portRaw)
            || string.IsNullOrWhiteSpace(secret)
            || !int.TryParse(portRaw, out var port)
            || port <= 0
            || port > 65535
        )
        {
            return false;
        }

        mtProxyUrl =
            $"https://t.me/proxy?server={Uri.EscapeDataString(server)}&port={port}&secret={Uri.EscapeDataString(secret)}";
        return true;
    }

    private static bool TryParseTelegramSocksLink(string proxyValue, out Uri proxyUri)
    {
        proxyUri = null!;

        if (!Uri.TryCreate(proxyValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.AbsolutePath.Trim('/').Equals("socks", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQueryString(uri.Query);
        if (
            !query.TryGetValue("server", out var server)
            || !query.TryGetValue("port", out var portRaw)
            || string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(portRaw)
            || !int.TryParse(portRaw, out var port)
            || port <= 0
            || port > 65535
        )
        {
            return false;
        }

        query.TryGetValue("user", out var user);
        query.TryGetValue("pass", out var pass);

        var userInfo = string.IsNullOrWhiteSpace(user)
            ? string.Empty
            : $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass ?? string.Empty)}@";

        var normalizedProxy = $"socks5://{userInfo}{server}:{port}";
        var isParsed = Uri.TryCreate(normalizedProxy, UriKind.Absolute, out var parsedProxyUri);
        if (isParsed && parsedProxyUri is not null)
        {
            proxyUri = parsedProxyUri;
            return true;
        }

        return false;
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return result;
        }

        var query = queryString.StartsWith('?') ? queryString[1..] : queryString;
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var decodedKey = Uri.UnescapeDataString(key.Replace('+', ' '));
            var decodedValue = Uri.UnescapeDataString(value.Replace('+', ' '));
            result[decodedKey] = decodedValue;
        }

        return result;
    }

    private static bool IsConnectivityException(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (current is SocketException || current is IOException || current is TimeoutException)
            {
                return true;
            }

            if (
                current is InvalidOperationException
                && ContainsConnectivityKeywords(current.Message)
            )
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static bool ContainsConnectivityKeywords(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        return normalized.Contains("connect")
            || normalized.Contains("connection")
            || normalized.Contains("proxy")
            || normalized.Contains("timeout")
            || normalized.Contains("timed out")
            || normalized.Contains("network")
            || normalized.Contains("socket")
            || normalized.Contains("соедин")
            || normalized.Contains("подключ");
    }

    private static async Task<TcpClient> CreateTcpClientThroughProxyAsync(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort
    )
    {
        return proxyUri.Scheme.ToLowerInvariant() switch
        {
            "socks5" => await ConnectThroughSocks5ProxyAsync(
                proxyUri,
                destinationAddress,
                destinationPort
            ),
            "http" => await ConnectThroughHttpProxyAsync(
                proxyUri,
                destinationAddress,
                destinationPort
            ),
            _ => throw new InvalidOperationException(
                $"Неподдерживаемая схема прокси: {proxyUri.Scheme}"
            ),
        };
    }

    private static async Task<TcpClient> ConnectThroughHttpProxyAsync(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort
    )
    {
        var tcpClient = new TcpClient();
        try
        {
            var proxyPort = proxyUri.IsDefaultPort ? 80 : proxyUri.Port;
            await tcpClient.ConnectAsync(proxyUri.Host, proxyPort);

            await using var stream = tcpClient.GetStream();

            var connectRequest = BuildHttpConnectRequest(
                proxyUri,
                destinationAddress,
                destinationPort
            );
            var requestBytes = Encoding.ASCII.GetBytes(connectRequest);
            await stream.WriteAsync(requestBytes);
            await stream.FlushAsync();

            var responseHeaders = await ReadHttpResponseHeadersAsync(stream);
            if (
                !responseHeaders.StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase)
                && !responseHeaders.StartsWith("HTTP/1.0 200", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    $"HTTP proxy CONNECT отклонен. Ответ прокси: {responseHeaders.Split("\r\n")[0]}"
                );
            }

            return tcpClient;
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }

    private static string BuildHttpConnectRequest(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort
    )
    {
        var destination = $"{destinationAddress}:{destinationPort}";
        var authHeader = string.Empty;

        if (!string.IsNullOrWhiteSpace(proxyUri.UserInfo))
        {
            var credentials = Uri.UnescapeDataString(proxyUri.UserInfo);
            var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            authHeader = $"Proxy-Authorization: Basic {encodedCredentials}\r\n";
        }

        return $"CONNECT {destination} HTTP/1.1\r\nHost: {destination}\r\nProxy-Connection: Keep-Alive\r\n{authHeader}\r\n";
    }

    private static async Task<string> ReadHttpResponseHeadersAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var responseBuffer = new List<byte>(4096);

        while (true)
        {
            var readCount = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (readCount == 0)
            {
                throw new IOException("HTTP proxy закрыл соединение во время CONNECT");
            }

            responseBuffer.AddRange(buffer.AsSpan(0, readCount).ToArray());

            if (responseBuffer.Count >= 4)
            {
                var count = responseBuffer.Count;
                if (
                    responseBuffer[count - 4] == '\r'
                    && responseBuffer[count - 3] == '\n'
                    && responseBuffer[count - 2] == '\r'
                    && responseBuffer[count - 1] == '\n'
                )
                {
                    return Encoding.ASCII.GetString(responseBuffer.ToArray());
                }
            }

            if (responseBuffer.Count > 64 * 1024)
            {
                throw new InvalidOperationException("Слишком длинный HTTP-ответ от прокси");
            }
        }
    }

    private static async Task<TcpClient> ConnectThroughSocks5ProxyAsync(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort
    )
    {
        var tcpClient = new TcpClient();
        try
        {
            var proxyPort = proxyUri.IsDefaultPort ? 1080 : proxyUri.Port;
            await tcpClient.ConnectAsync(proxyUri.Host, proxyPort);

            await using var stream = tcpClient.GetStream();

            var hasCredentials = !string.IsNullOrWhiteSpace(proxyUri.UserInfo);
            var greeting = hasCredentials
                ? new byte[] { 0x05, 0x02, 0x00, 0x02 }
                : new byte[] { 0x05, 0x01, 0x00 };
            await stream.WriteAsync(greeting);

            var methodResponse = await ReadExactAsync(stream, 2);
            if (methodResponse[0] != 0x05)
            {
                throw new InvalidOperationException("Неверный ответ SOCKS5 прокси");
            }

            if (methodResponse[1] == 0xFF)
            {
                throw new InvalidOperationException(
                    "SOCKS5 прокси не поддерживает доступные методы аутентификации"
                );
            }

            if (methodResponse[1] == 0x02)
            {
                await AuthenticateSocks5Async(stream, proxyUri);
            }
            else if (methodResponse[1] != 0x00)
            {
                throw new InvalidOperationException(
                    $"SOCKS5 прокси вернул неподдерживаемый метод: {methodResponse[1]}"
                );
            }

            var connectRequest = BuildSocks5ConnectRequest(destinationAddress, destinationPort);
            await stream.WriteAsync(connectRequest);

            var connectResponseHead = await ReadExactAsync(stream, 4);
            if (connectResponseHead[0] != 0x05)
            {
                throw new InvalidOperationException("Неверный ответ SOCKS5 при CONNECT");
            }

            if (connectResponseHead[1] != 0x00)
            {
                throw new InvalidOperationException(
                    $"SOCKS5 CONNECT завершился ошибкой: {connectResponseHead[1]}"
                );
            }

            await ConsumeSocks5BindAddressAsync(stream, connectResponseHead[3]);

            return tcpClient;
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }

    private static async Task AuthenticateSocks5Async(NetworkStream stream, Uri proxyUri)
    {
        var credentials = Uri.UnescapeDataString(proxyUri.UserInfo ?? string.Empty);
        var separatorIndex = credentials.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException(
                "Для SOCKS5-аутентификации требуется формат user:password"
            );
        }

        var username = credentials[..separatorIndex];
        var password = credentials[(separatorIndex + 1)..];
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        if (usernameBytes.Length > byte.MaxValue || passwordBytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException(
                "Логин или пароль SOCKS5 превышают максимально допустимую длину"
            );
        }

        var authRequest = new byte[3 + usernameBytes.Length + passwordBytes.Length];
        authRequest[0] = 0x01;
        authRequest[1] = (byte)usernameBytes.Length;
        Buffer.BlockCopy(usernameBytes, 0, authRequest, 2, usernameBytes.Length);
        authRequest[2 + usernameBytes.Length] = (byte)passwordBytes.Length;
        Buffer.BlockCopy(
            passwordBytes,
            0,
            authRequest,
            3 + usernameBytes.Length,
            passwordBytes.Length
        );

        await stream.WriteAsync(authRequest);

        var authResponse = await ReadExactAsync(stream, 2);
        if (authResponse[1] != 0x00)
        {
            throw new InvalidOperationException("SOCKS5-аутентификация отклонена прокси");
        }
    }

    private static byte[] BuildSocks5ConnectRequest(string destinationAddress, int destinationPort)
    {
        var portBytes = new[]
        {
            (byte)((destinationPort >> 8) & 0xFF),
            (byte)(destinationPort & 0xFF),
        };

        if (IPAddress.TryParse(destinationAddress, out var ipAddress))
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var addressType = ipBytes.Length == 16 ? (byte)0x04 : (byte)0x01;
            var request = new byte[4 + ipBytes.Length + 2];
            request[0] = 0x05;
            request[1] = 0x01;
            request[2] = 0x00;
            request[3] = addressType;
            Buffer.BlockCopy(ipBytes, 0, request, 4, ipBytes.Length);
            Buffer.BlockCopy(portBytes, 0, request, 4 + ipBytes.Length, 2);
            return request;
        }

        var hostBytes = Encoding.ASCII.GetBytes(destinationAddress);
        if (hostBytes.Length == 0 || hostBytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException("Некорректный адрес назначения для SOCKS5 CONNECT");
        }

        var domainRequest = new byte[5 + hostBytes.Length + 2];
        domainRequest[0] = 0x05;
        domainRequest[1] = 0x01;
        domainRequest[2] = 0x00;
        domainRequest[3] = 0x03;
        domainRequest[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, domainRequest, 5, hostBytes.Length);
        Buffer.BlockCopy(portBytes, 0, domainRequest, 5 + hostBytes.Length, 2);
        return domainRequest;
    }

    private static async Task ConsumeSocks5BindAddressAsync(NetworkStream stream, byte addressType)
    {
        var addressLength = addressType switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => (await ReadExactAsync(stream, 1))[0],
            _ => throw new InvalidOperationException(
                $"Неизвестный тип адреса SOCKS5: {addressType}"
            ),
        };

        await ReadExactAsync(stream, addressLength + 2);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var readCount = await stream.ReadAsync(buffer, offset, count - offset);
            if (readCount == 0)
            {
                throw new IOException("Прокси неожиданно закрыл соединение");
            }

            offset += readCount;
        }

        return buffer;
    }

    private async Task<string?> GetProxyValueFromRootStateAsync(CancellationToken cancellationToken)
    {
        string? proxyValue = null;

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

            var proxyState = await dbContext
                .RootState.AsNoTracking()
                .SingleOrDefaultAsync(
                    state => state.Name == RootStateKeys.WTelegramProxyUrl,
                    cancellationToken
                );

            if (proxyState is null || string.IsNullOrWhiteSpace(proxyState.Value))
            {
                proxyState = await dbContext
                    .RootState.AsNoTracking()
                    .SingleOrDefaultAsync(
                        state => state.Name == RootStateKeys.WTelegramMtProxyUrl,
                        cancellationToken
                    );
            }

            if (!string.IsNullOrWhiteSpace(proxyState?.Value))
            {
                proxyValue = proxyState.Value.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Не удалось прочитать настройки прокси WTelegram из RootState. Клиент продолжит работу без прокси"
            );
        }

        return proxyValue;
    }

    #region Notification Methods

    /// <summary>
    /// Уведомляет администратора о необходимости повторной авторизации WTelegram
    /// </summary>
    private async Task NotifyAuthRequiredAsync(string reason = "AUTH_KEY_UNREGISTERED")
    {
        try
        {
            var message = $"""
                ⚠️ <b>WTelegram требует переавторизацию!</b>

                <b>Причина:</b> {reason}

                📝 <b>Что нужно сделать:</b>
                1. Зайдите в консоль приложения
                2. Введите код верификации, который придет в Telegram
                3. Или используйте API: <code>POST /api/wtelegram/relogin</code>

                <b>Статус можно проверить:</b>
                <code>GET /api/wtelegram/status</code>
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation(
                "Уведомление о необходимости переавторизации WTelegram отправлено администратору"
            );

            WTelegramOperationResult.CreateSuccess("Notification sent successfully");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления о переавторизации WTelegram");
            WTelegramOperationResult.CreateFailure("Failed to send notification", e.Message);
        }
    }

    /// <summary>
    /// Уведомляет администратора об успешной переавторизации
    /// </summary>
    private async Task NotifyAuthSuccessAsync(string username)
    {
        try
        {
            var message = $"""
                ✅ <b>WTelegram успешно переавторизован!</b>

                <b>Пользователь:</b> {username}

                Сессия восстановлена, все функции работают в штатном режиме.
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation("Уведомление об успешной переавторизации WTelegram отправлено");

            WTelegramOperationResult.CreateSuccess("Success notification sent");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления об успешной авторизации");
            WTelegramOperationResult.CreateFailure(
                "Failed to send success notification",
                e.Message
            );
        }
    }

    /// <summary>
    /// Уведомляет администратора о необходимости ввода кода верификации
    /// </summary>
    private async Task NotifyVerificationCodeRequiredAsync()
    {
        try
        {
            var message = """
                🔐 <b>WTelegram ожидает код верификации!</b>

                📱 <b>Действия:</b>
                1. Проверить Telegram на предмет сообщения с кодом
                2. Открыть консоль приложения
                3. Ввести полученный код

                ⏰ <b>Важно:</b> Процесс авторизации приостановлен до ввода кода.
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation("Уведомление о необходимости кода верификации отправлено");

            WTelegramOperationResult.CreateSuccess("Verification code notification sent");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления о коде верификации");
            WTelegramOperationResult.CreateFailure(
                "Failed to send verification code notification",
                e.Message
            );
        }
    }

    /// <summary>
    /// Уведомляет администратора об ошибке авторизации
    /// </summary>
    private async Task NotifyAuthFailedAsync(string errorMessage)
    {
        try
        {
            var message = $"""
                ❌ <b>Ошибка авторизации WTelegram!</b>

                <b>Сообщение об ошибке:</b>
                <code>{errorMessage}</code>

                🔧 <b>Рекомендуется:</b>
                1. Проверить настройки конфигурации
                2. Убедиться в правильности AppId и ApiHash
                3. Попробовать переавторизацию через API

                <code>POST /api/wtelegram/relogin</code>
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation("Уведомление об ошибке авторизации WTelegram отправлено");

            WTelegramOperationResult.CreateSuccess("Error notification sent");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления об ошибке авторизации");
            WTelegramOperationResult.CreateFailure("Failed to send error notification", e.Message);
        }
    }

    private async Task NotifyProxyConnectivityCheckOnceAsync(
        string proxyDescription,
        string proxyErrorMessage,
        bool worksWithoutProxy,
        string? noProxyErrorMessage
    )
    {
        if (Interlocked.CompareExchange(ref _proxyConnectivityNotificationSent, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var proxyErrorEncoded = WebUtility.HtmlEncode(proxyErrorMessage);
            var withoutProxyResult = worksWithoutProxy
                ? "✅ Без прокси подключение к серверам Telegram успешно"
                : $"❌ Без прокси подключение тоже не удалось: <code>{WebUtility.HtmlEncode(noProxyErrorMessage ?? "Нет деталей")}</code>";

            var message = $"""
                ⚠️ <b>Диагностика подключения WTelegram</b>

                <b>Прокси:</b> {WebUtility.HtmlEncode(proxyDescription)}
                <b>Ошибка через прокси:</b>
                <code>{proxyErrorEncoded}</code>

                {withoutProxyResult}
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation(
                "Отправлено единичное уведомление диагностики подключения WTelegram"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось отправить диагностику подключения WTelegram");
        }
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _client?.Dispose();
        _loginLock.Dispose();
        GC.SuppressFinalize(this);
        _isDisposed = true;
    }
}
