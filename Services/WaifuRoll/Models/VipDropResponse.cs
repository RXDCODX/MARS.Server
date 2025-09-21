namespace MARS.Server.Services.WaifuRoll.Models;

/// <summary>
/// Ответ на проверку выпадения VIP статуса
/// </summary>
public class VipDropResponse
{
    /// <summary>
    /// Выпал ли VIP статус
    /// </summary>
    public bool IsVipDropped { get; set; }

    /// <summary>
    /// Количество роллов пользователя
    /// </summary>
    public int RollCount { get; set; }

    /// <summary>
    /// Причина выпадения (гарант или случайность)
    /// </summary>
    public string? DropReason { get; set; }
}
