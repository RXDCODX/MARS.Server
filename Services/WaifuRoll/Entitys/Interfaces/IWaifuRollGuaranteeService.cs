namespace MARS.Server.Services.WaifuRoll.Entitys.Interfaces;

public interface IWaifuRollGuaranteeService
{
    /// <summary>
    /// Проверяет, выпал ли VIP статус пользователю
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>True если VIP статус выпал, иначе False</returns>
    Task<bool> CheckVipDropAsync(string twitchId);

    /// <summary>
    /// Увеличивает счетчик роллов пользователя
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>True если операция выполнена успешно</returns>
    Task<bool> IncrementRollCountAsync(string twitchId);

    /// <summary>
    /// Получает информацию о гаранте пользователя
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Информация о гаранте или null если не найден</returns>
    Task<WaifuRollGuarantee?> GetGuaranteeInfoAsync(string twitchId);

    /// <summary>
    /// Сбрасывает счетчик роллов пользователя (при выпадении VIP)
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>True если операция выполнена успешно</returns>
    Task<bool> ResetRollCountAsync(string twitchId);
}
