using MARS.Server.Services.WaifuRoll.Entitys;

namespace MARS.Server.Services.WaifuRoll.Models;

/// <summary>
/// Ответ на запрос ролла вайфу через Telegram
/// </summary>
public class TelegramRollWaifuResponse
{
    /// <summary>
    /// Выпавшая вайфу
    /// </summary>
    public Waifu? Waifu { get; set; }

    /// <summary>
    /// Хост, который заказал вайфу
    /// </summary>
    public Husband? Host { get; set; }

    /// <summary>
    /// Муж/жена вайфу (если вайфу приватизирована)
    /// </summary>
    public Husband? Husband { get; set; }
}
