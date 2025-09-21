using MARS.Server.Services.WaifuRoll.Models;

namespace MARS.Server.Services.WaifuRoll.Entitys.Interfaces;

public interface IWaifuRollGuaranteeService
{
    /// <summary>
    /// Проверяет, выпал ли VIP статус пользователю
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Результат проверки VIP статуса</returns>
    Task<OperationResult<VipDropResponse>> CheckVipDropAsync(string twitchId);

    /// <summary>
    /// Увеличивает счетчик роллов пользователя
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Результат увеличения счетчика</returns>
    Task<OperationResult<RollCountResponse>> IncrementRollCountAsync(string twitchId);

    /// <summary>
    /// Получает информацию о гаранте пользователя
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Информация о гаранте</returns>
    Task<OperationResult<WaifuRollGuarantee?>> GetGuaranteeInfoAsync(string twitchId);

    /// <summary>
    /// Сбрасывает счетчик роллов пользователя (при выпадении VIP)
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Результат сброса счетчика</returns>
    Task<OperationResult<RollCountResponse>> ResetRollCountAsync(string twitchId);

    /// <summary>
    /// Удаляет пользователя из системы гарантов (при выпадении VIP по гаранту)
    /// </summary>
    /// <param name="twitchId">Twitch ID пользователя</param>
    /// <returns>Результат удаления</returns>
    Task<OperationResult<bool>> DeleteGuaranteeAsync(string twitchId);
}
