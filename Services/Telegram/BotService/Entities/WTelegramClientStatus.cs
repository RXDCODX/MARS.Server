namespace MARS.Server.Services.Telegram.BotService.Entities;

/// <summary>
/// Статус авторизации WTelegram клиента
/// </summary>
public class WTelegramClientStatus
{
    /// <summary>
    /// Указывает, авторизован ли клиент
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// ID пользователя Telegram
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Username пользователя (если доступен)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Номер телефона пользователя (если доступен)
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Информация об ошибке (если есть)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Указывает, что клиент ожидает ввода кода верификации
    /// </summary>
    public bool IsAwaitingCode { get; set; }
}
