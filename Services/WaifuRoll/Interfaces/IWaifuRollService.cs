using System.Threading.Tasks;
using MARS.Server.Services.WaifuRoll.Entitys;
using MARS.Server.Services.WaifuRoll.Models;
using ShikimoriSharp.Classes;

namespace MARS.Server.Services.WaifuRoll.Interfaces;

public interface IWaifuRollService
{
    /// <summary>
    /// Ролл вайфу для пользователя
    /// </summary>
    /// <param name="id">Twitch ID пользователя</param>
    /// <param name="displayName">Отображаемое имя</param>
    /// <param name="forcePass">Принудительный пропуск кулдауна</param>
    /// <returns>Выпавшая вайфу или null</returns>
    Task<Waifu?> RollTheWaifu(string id, string? displayName = null, bool forcePass = false);

    /// <summary>
    /// Ролл вайфу через Telegram
    /// </summary>
    /// <param name="name">Имя хоста</param>
    /// <returns>Результат ролла вайфу</returns>
    Task<OperationResult<TelegramRollWaifuResponse>> TelegramRollWaifu(string name);

    /// <summary>
    /// Добавление новой вайфу
    /// </summary>
    /// <param name="character">Персонаж из Shikimori</param>
    /// <returns>Результат добавления вайфу</returns>
    Task<OperationResult<AddNewWaifuResponse>> AddNewWaifu(FullCharacter character);

    /// <summary>
    /// Объединение вайфу с хостом
    /// </summary>
    /// <param name="host">Хост</param>
    /// <param name="waifu">Вайфу</param>
    /// <param name="makeprivate">Сделать приватной</param>
    /// <returns>Успешность операции</returns>
    Task<bool> MergeTheWaifu(Host host, Waifu waifu, bool makeprivate = true);

    /// <summary>
    /// Автоматическое приветствие
    /// </summary>
    /// <param name="id">Twitch ID</param>
    /// <param name="displayName">Отображаемое имя</param>
    /// <returns>Сообщение приветствия или null</returns>
    Task<string?> AutoHello(string id, string displayName);
}
