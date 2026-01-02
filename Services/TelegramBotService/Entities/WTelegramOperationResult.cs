namespace MARS.Server.Services.TelegramBotService.Entities;

/// <summary>
/// Результат операции с WTelegram клиентом
/// </summary>
public class WTelegramOperationResult
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение результата операции
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Детали ошибки (если операция не удалась)
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Дополнительная информация статуса клиента (если применимо)
    /// </summary>
    public WTelegramClientStatus? ClientStatus { get; set; }

    /// <summary>
    /// Создает успешный результат
    /// </summary>
    public static WTelegramOperationResult CreateSuccess(
        string message,
        WTelegramClientStatus? clientStatus = null
    ) => new()
    {
        Success = true,
        Message = message,
        ClientStatus = clientStatus
    };

    /// <summary>
    /// Создает результат с ошибкой
    /// </summary>
    public static WTelegramOperationResult CreateFailure(string message, string? errorDetails = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorDetails = errorDetails
        };
}
