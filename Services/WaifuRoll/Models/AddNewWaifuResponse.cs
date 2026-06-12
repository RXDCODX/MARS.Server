using MARS.Server.Services.WaifuRoll.Entitys;

namespace MARS.Server.Services.WaifuRoll.Models;

/// <summary>
/// Ответ на запрос добавления новой вайфу
/// </summary>
public class AddNewWaifuResponse
{
    /// <summary>
    /// Добавленная вайфу
    /// </summary>
    public Waifu? Waifu { get; set; }

    /// <summary>
    /// Флаг ошибки при добавлении
    /// </summary>
    public bool HasError { get; set; }
}
