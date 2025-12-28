using MARS.Server.Configuration;
using MARS.Server.Exstensions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TL;

namespace MARS.Server.Services.TelegramBotService;

/// <summary>
/// Сервис-обертка для WTelegram.Client с автоматической переавторизацией
/// </summary>
public class WTelegramClientService : IDisposable
{
    private readonly ILogger<WTelegramClientService> _logger;
    private readonly WTelegramClientConfiguration _configuration;
    private readonly ITelegramBotClient? _botClient;
    private readonly string _sessionPath;
    private WTelegramClient? _client;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _isDisposed;

    public WTelegramClientService(
        ILogger<WTelegramClientService> logger,
        IOptions<WTelegramClientConfiguration> configuration,
        ITelegramBotClient? botClient = null
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
            _client?.Dispose();
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

            switch (whatIsNeeded)
            {
                case "verification_code":
                    _logger.LogWarning("ТРЕБУЕТСЯ КОД ВЕРИФИКАЦИИ! Введите код в консоль...");

                    await NotifyVerificationCodeRequiredAsync();

                    Console.Write("Код верификации: ");
                    loginInfo = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(loginInfo))
                    {
                        throw new InvalidOperationException("Код верификации не был предоставлен");
                    }
                    break;

                case "name":
                    loginInfo = _configuration.FirstNameLastName;
                    _logger.LogInformation("Используется имя: {Name}", loginInfo);
                    break;

                case "password":
                    loginInfo = _configuration.Password;
                    _logger.LogInformation("Используется пароль 2FA");
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

    #region Notification Methods

    /// <summary>
    /// Уведомляет администратора о необходимости повторной авторизации WTelegram
    /// </summary>
    private async Task NotifyAuthRequiredAsync(string reason = "AUTH_KEY_UNREGISTERED")
    {
        if (_botClient == null)
        {
            return;
        }

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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления о переавторизации WTelegram");
        }
    }

    /// <summary>
    /// Уведомляет администратора об успешной переавторизации
    /// </summary>
    private async Task NotifyAuthSuccessAsync(string username)
    {
        if (_botClient == null)
        {
            return;
        }

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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления об успешной авторизации");
        }
    }

    /// <summary>
    /// Уведомляет администратора о необходимости ввода кода верификации
    /// </summary>
    private async Task NotifyVerificationCodeRequiredAsync()
    {
        if (_botClient == null)
        {
            return;
        }

        try
        {
            var message = """
                🔐 <b>WTelegram ожидает код верификации!</b>

                📱 <b>Действия:</b>
                1. Проверьте Telegram на предмет сообщения с кодом
                2. Откройте консоль приложения
                3. Введите полученный код

                ⏰ <b>Важно:</b> Процесс авторизации приостановлен до ввода кода.
                """;

            await _botClient.SendMessage(
                TelegramExstension.Rxdcodx,
                message,
                parseMode: ParseMode.Html
            );

            _logger.LogInformation("Уведомление о необходимости кода верификации отправлено");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления о коде верификации");
        }
    }

    /// <summary>
    /// Уведомляет администратора об ошибке авторизации
    /// </summary>
    private async Task NotifyAuthFailedAsync(string errorMessage)
    {
        if (_botClient == null)
        {
            return;
        }

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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка при отправке уведомления об ошибке авторизации");
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
        _isDisposed = true;
    }
}
