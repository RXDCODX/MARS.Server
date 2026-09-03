global using WTelegramClient = WTelegram.Client;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly TelegramProxyConfiguration _proxyConfiguration;
    private WTelegramClient? _client;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _isDisposed;

    private readonly ITelegramBotClient _botClient;
    private readonly ManualResetEventSlim _codeWaitHandle = new(false);
    private string? _pendingVerificationCode;
    private volatile bool _awaitingCode;
    private int _proxyConnectivityNotificationSent;
    private readonly string _serverBaseUrl;

    private sealed class ProxyConfigurationInfo
    {
        public bool IsProxyConfigured { get; init; }
        public string Description { get; init; } = "Без прокси";
    }

    public WTelegramClientService(
        ILogger<WTelegramClientService> logger,
        IOptions<WTelegramClientConfiguration> configuration,
        IOptions<TelegramProxyConfiguration> proxyConfiguration,
        ITelegramBotClient botClient,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IConfiguration appConfiguration
    )
    {
        _logger = logger;
        _configuration = configuration.Value;
        _proxyConfiguration = proxyConfiguration.Value;
        _botClient = botClient;
        _dbContextFactory = dbContextFactory;
        _serverBaseUrl = BuildServerBaseUrl(appConfiguration);

        Helpers.Log = (level, message) => _logger.Log((LogLevel)level, "{Message}", message);
    }

    private static string BuildServerBaseUrl(IConfiguration configuration)
    {
        var urls = configuration["urls"];
        if (
            !string.IsNullOrWhiteSpace(urls)
            && Uri.TryCreate(urls.Replace("*", "localhost"), UriKind.Absolute, out var uri)
        )
        {
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }

        return "http://localhost:9255";
    }

    /// <summary>
    /// Обрабатывает обновления от Telegram Bot для получения экземпляра ITelegramBotClient
    /// </summary>
    public Task HandleUpdate(ITelegramBotClient _, Update? update)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Получает статус авторизации WTelegram клиента
    /// </summary>
    public Task<WTelegramClientStatus> GetClientStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            try
            {
                if (_client?.User != null)
                {
                    return Task.FromResult(
                        new WTelegramClientStatus
                        {
                            IsAuthenticated = true,
                            UserId = _client.User.id,
                            Username = _client.User.username,
                            Phone = _client.User.phone,
                            IsAwaitingCode = _awaitingCode,
                        }
                    );
                }

                return Task.FromResult(
                    new WTelegramClientStatus
                    {
                        IsAuthenticated = false,
                        IsAwaitingCode = _awaitingCode,
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статуса WTelegram");
                return Task.FromResult(
                    new WTelegramClientStatus
                    {
                        IsAuthenticated = false,
                        ErrorMessage = ex.Message,
                        IsAwaitingCode = _awaitingCode,
                    }
                );
            }
        }
        catch (Exception exception)
        {
            return Task.FromException<WTelegramClientStatus>(exception);
        }
    }

    /// <summary>
    /// Подтверждает код верификации, полученный через веб-интерфейс
    /// </summary>
    public bool SubmitVerificationCode(string code)
    {
        if (!_awaitingCode)
        {
            _logger.LogWarning("Попытка отправить код верификации, когда он не ожидается");
            return false;
        }

        _pendingVerificationCode = code;
        _codeWaitHandle.Set();
        _logger.LogInformation("Код верификации принят через веб-интерфейс");
        return true;
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

        _logger.LogWarning("Требуется код верификации. Ожидание ввода через веб-интерфейс...");

        NotifyVerificationCodeRequiredAsync().GetAwaiter().GetResult();

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

        await InvokeWithFloodWaitRetryAsync(
            async () =>
            {
                var user = await client.LoginUserIfNeeded();

                _logger.LogInformation("Авторизация WTelegram успешна. Пользователь: {User}", user);

                var username = user?.username ?? user?.phone ?? "Unknown";
                await NotifyAuthSuccessAsync(username);
                Interlocked.Exchange(ref _proxyConnectivityNotificationSent, 0);
            },
            cancellationToken
        );
    }

    private async Task InvokeWithFloodWaitRetryAsync(
        Func<Task> action,
        CancellationToken cancellationToken
    )
    {
        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (TL.RpcException ex)
                when (ex.Message.Contains("FLOOD_WAIT_", StringComparison.Ordinal)
                    && int.TryParse(
                        ex.Message.AsSpan(
                            ex.Message.IndexOf("FLOOD_WAIT_", StringComparison.Ordinal) + 11
                        ),
                        out var waitSeconds
                    )
                )
            {
                if (attempt < maxRetries - 1)
                {
                    var delaySeconds = waitSeconds + 1;
                    _logger.LogWarning(
                        "FLOOD_WAIT_{WaitSeconds}: ожидание {DelaySeconds} сек перед повтором (попытка {Attempt}/{MaxRetries})",
                        waitSeconds,
                        delaySeconds,
                        attempt + 1,
                        maxRetries
                    );
                    await Task.Delay(delaySeconds * 1000, cancellationToken);
                }
                else
                {
                    throw;
                }
            }
        }
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

    private Task<ProxyConfigurationInfo> ApplyProxyConfigurationAsync(
        CancellationToken cancellationToken
    )
    {
        var result = new ProxyConfigurationInfo();

        var proxyValue = !string.IsNullOrWhiteSpace(_proxyConfiguration.WTelegramProxyUrl)
            ? _proxyConfiguration.WTelegramProxyUrl.Trim()
            : null;

        if (!string.IsNullOrWhiteSpace(proxyValue) && _client is not null)
        {
            if (
                TelegramProxyHelper.TryParseTelegramProxyLink(
                    proxyValue,
                    out var mtProxyUrlFromTelegramLink
                )
            )
            {
                _client.MTProxyUrl = mtProxyUrlFromTelegramLink;
                _logger.LogInformation(
                    "Для WTelegram включен MTProxy из Telegram-ссылки"
                );

                result = new ProxyConfigurationInfo
                {
                    IsProxyConfigured = true,
                    Description = "MTProxy (t.me/proxy)",
                };
            }
            else if (TelegramProxyHelper.IsMtProxyUrl(proxyValue))
            {
                _client.MTProxyUrl = proxyValue;
                _logger.LogInformation("Для WTelegram включен MTProxy из конфигурации");

                result = new ProxyConfigurationInfo
                {
                    IsProxyConfigured = true,
                    Description = "MTProxy",
                };
            }
            else
            {
                var forceType = TelegramProxyHelper.ReadWTelegramProxyType(_proxyConfiguration);
                var proxyUri = TelegramProxyHelper.ResolveHttpProxyUri(proxyValue, forceType);

                if (proxyUri is not null)
                {
                    _client.TcpHandler = (destinationAddress, destinationPort) =>
                        CreateTcpClientThroughProxyAsync(
                            proxyUri,
                            destinationAddress,
                            destinationPort
                        );

                    var description = forceType is not null
                        ? $"{forceType.ToUpperInvariant()} proxy (forced)"
                        : $"{proxyUri.Scheme.ToUpperInvariant()} proxy";

                    _logger.LogInformation(
                        "Для WTelegram включен прокси {Description} из конфигурации",
                        description
                    );

                    result = new ProxyConfigurationInfo
                    {
                        IsProxyConfigured = true,
                        Description = description,
                    };
                }
                else
                {
                    _logger.LogWarning(
                        "Некорректный формат прокси для WTelegram в конфигурации. Ожидается MTProxy URL, socks5:// или http://"
                    );
                }
            }
        }

        return Task.FromResult(result);
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
            "socks5" => await TelegramProxyHelper.ConnectThroughSocks5ProxyAsync(
                proxyUri,
                destinationAddress,
                destinationPort
            ),
            "http" => await TelegramProxyHelper.ConnectThroughHttpProxyAsync(
                proxyUri,
                destinationAddress,
                destinationPort
            ),
            _ => throw new InvalidOperationException(
                $"Неподдерживаемая схема прокси: {proxyUri.Scheme}"
            ),
        };
    }

    #region Notification Methods

    /// <summary>
    /// Уведомляет администратора о необходимости повторной авторизации WTelegram
    /// </summary>
    private async Task NotifyAuthRequiredAsync(string reason = "AUTH_KEY_UNREGISTERED")
    {
        try
        {
            var wtelegramUrl = $"{_serverBaseUrl}/wtelegram";
            var message = $"""
                ⚠️ <b>WTelegram требует переавторизацию!</b>

                <b>Причина:</b> {reason}

                📝 <b>Что нужно сделать:</b>
                1. Открыть <a href="{wtelegramUrl}">веб-панель WTelegram</a>
                2. Ввести код верификации, который придет в Telegram
                3. Нажать "Отправить"

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
            var wtelegramUrl = $"{_serverBaseUrl}/wtelegram";
            var message = $"""
                🔐 <b>WTelegram ожидает код верификации!</b>

                📱 <b>Действия:</b>
                1. Проверить Telegram на предмет сообщения с кодом
                2. Открыть <a href="{wtelegramUrl}">веб-панель WTelegram</a>
                3. Ввести полученный код и нажать "Отправить"

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
            var wtelegramUrl = $"{_serverBaseUrl}/wtelegram";
            var message = $"""
                ❌ <b>Ошибка авторизации WTelegram!</b>

                <b>Сообщение об ошибке:</b>
                <code>{errorMessage}</code>

                🔧 <b>Рекомендуется:</b>
                1. Проверить настройки конфигурации
                2. Убедиться в правильности AppId и ApiHash
                3. Попробовать <a href="{wtelegramUrl}">переавторизацию через веб-панель</a>
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
