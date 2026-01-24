namespace MARS.Server.Services.TelegramBotService.Entities;

/// <summary>
/// Требование для процесса авторизации WTelegram
/// </summary>
public enum WTelegramAuthenticationRequirement
{
    /// <summary>
    /// Требуется ввод номера телефона
    /// </summary>
    PhoneNumber,

    /// <summary>
    /// Требуется ввод кода верификации
    /// </summary>
    VerificationCode,

    /// <summary>
    /// Требуется ввод имени (для новых аккаунтов)
    /// </summary>
    Name,

    /// <summary>
    /// Требуется ввод пароля двухфакторной авторизации
    /// </summary>
    Password,

    /// <summary>
    /// Авторизация завершена успешно
    /// </summary>
    Completed,

    /// <summary>
    /// Неизвестное требование
    /// </summary>
    Unknown,
}
