namespace MARS.Server.Services.Telegram.BotService.Entities;

/// <summary>
/// Запрос на отправку кода верификации
/// </summary>
public class VerificationCodeRequest
{
    /// <summary>
    /// Код верификации из Telegram
    /// </summary>
    public required string Code { get; set; }
}
