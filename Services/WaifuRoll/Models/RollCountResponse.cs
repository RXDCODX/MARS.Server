namespace MARS.Server.Services.WaifuRoll.Models;

/// <summary>
/// Ответ на операцию с счетчиком роллов
/// </summary>
public class RollCountResponse
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Текущее количество роллов
    /// </summary>
    public int CurrentRollCount { get; set; }

    /// <summary>
    /// Сообщение об операции
    /// </summary>
    public string? Message { get; set; }
}
