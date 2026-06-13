using MARS.Server.Services.WaifuRoll.Entitys;
using Host = MARS.Server.Services.WaifuRoll.Entitys.Host;

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
    public Host? Host { get; set; }

    /// <summary>
    /// Муж/жена вайфу (если вайфу приватизирована)
    /// </summary>
    public Host? Husband { get; set; }
}
