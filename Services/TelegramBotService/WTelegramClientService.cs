using MARS.Server.Services.TelegramBotService.Entities;
using Telegram.Bot.Types.Enums;

namespace MARS.Server.Services.TelegramBotService;

/// <summary>
/// Сервис-обертка для WTelegramClient с автоматической переавторизацией
/// </summary>
public class WTelegramClientService : IDisposable
{
    private readonly ILogger<WTelegramClientService> _logger;
    private readonly WTelegramClientConfiguration _configuration;
    private readonly string _sessionPath;
    private WTelegramClient? _client;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _isDisposed;

    private readonly ITelegramBotClient _botClient;
    private TaskCompletionSource<string>? _verificationCodeTcs;

    public WTelegramClientService(
        ILogger<WTelegramClientService> logger,
        IOptions<WTelegramClientConfiguration> configuration,
        ITelegramBotClient botClient
    )
    {
        _logger = logger;
        _configuration = configuration.Value;
        _botClient = botClient;
        _sessionPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "WTelegram",
            "WTelegram.session"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);

        WTelegram.Helpers.Log = (level, message) =>
            _logger.Log((LogLevel)level, "{Message}", message);
    }

    /// <summary>
    /// Обрабатывает обновления от Telegram Bot для получения экземпляра ITelegramBotClient
    /// </summary>
    public Task HandleUpdate(ITelegramBotClient _, Update? update)
    {
        // Кэшируем экземпляр клиента при первом обновлении
        _logger.LogInformation("WTelegramClientService получил экземпляр ITelegramBotClient");

        if (
            update?.Message?.Text is { } text
            && _verificationCodeTcs is { Task.IsCompleted: false } pendingCode
        )
        {
            pendingCode.TrySetResult(text);
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

            // Удаляем старую сессию
            if (File.Exists(_sessionPath))
            {
                try
                {
                    File.Delete(_sessionPath);
                    _logger.LogInformation("Старая сессия удалена");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось удалить старую сессию");
                }
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

            await NotifyAuthFailedAsync(ex.Message);

            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task InitializeClientAsync(CancellationToken cancellationToken)
    {
        _client = new WTelegramClient(_configuration.AppId, _configuration.ApiHash, _sessionPath);

        var loginInfo = _configuration.PhoneNumber;

        while (_client.User == null)
        {
            var whatIsNeeded = await _client.Login(loginInfo);

            _logger.LogInformation("WTelegram требует: {WhatIsNeeded}", whatIsNeeded);

            var requirement = ParseAuthenticationRequirement(whatIsNeeded);

            switch (requirement)
            {
                case WTelegramAuthenticationRequirement.VerificationCode:
                    _logger.LogWarning("ТРЕБУЕТСЯ КОД ВЕРИФИКАЦИИ! Введите код в консоль...");

                    await NotifyVerificationCodeRequiredAsync();

                    if (_botClient is not null)
                    {
                        await _botClient.SendMessage(
                            TelegramExstension.Rxdcodx,
                            "Введите код верификации:",
                            cancellationToken: cancellationToken
                        );
                    }

                    loginInfo = await WaitForVerificationCodeAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(loginInfo))
                    {
                        throw new InvalidOperationException("Код верификации не был предоставлен");
                    }
                    break;

                case WTelegramAuthenticationRequirement.Name:
                    loginInfo = _configuration.FirstNameLastName;
                    _logger.LogInformation("Используется имя: {Name}", loginInfo);
                    break;

                case WTelegramAuthenticationRequirement.Password:
                    loginInfo = _configuration.Password;
                    _logger.LogInformation("Используется пароль 2FA");
                    break;

                case WTelegramAuthenticationRequirement.PhoneNumber:
                    loginInfo = _configuration.PhoneNumber;
                    _logger.LogInformation("Используется номер телефона: {PhoneNumber}", loginInfo);
                    break;
                case WTelegramAuthenticationRequirement.Completed:
                    break;
                case WTelegramAuthenticationRequirement.Unknown:
                    break;
                default:
                    loginInfo = string.Empty;
                    break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "Авторизация была отменена",
                    cancellationToken
                );
            }
        }

        _logger.LogInformation("Авторизация WTelegram успешна. Пользователь: {User}", _client.User);

        var username = _client.User?.username ?? _client.User?.phone ?? "Unknown";
        await NotifyAuthSuccessAsync(username);
    }

    private static WTelegramAuthenticationRequirement ParseAuthenticationRequirement(
        string requirement
    ) =>
        requirement switch
        {
            "verification_code" => WTelegramAuthenticationRequirement.VerificationCode,
            "name" => WTelegramAuthenticationRequirement.Name,
            "password" => WTelegramAuthenticationRequirement.Password,
            _ => WTelegramAuthenticationRequirement.Unknown,
        };

    #region Notification Methods

    private async Task<string> WaitForVerificationCodeAsync(CancellationToken cancellationToken)
    {
        _verificationCodeTcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        await using var registration = cancellationToken.Register(() =>
        {
            _verificationCodeTcs?.TrySetCanceled(cancellationToken);
        });

        return await _verificationCodeTcs.Task;
    }

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
