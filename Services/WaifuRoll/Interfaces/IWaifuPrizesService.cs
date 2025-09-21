using MARS.Server.Services.WaifuRoll.Entitys;

namespace MARS.Server.Services.WaifuRoll.Interfaces;

public interface IWaifuPrizesService
{
    /// <summary>
    /// Получение призов вайфу
    /// </summary>
    /// <returns>Результат получения призов</returns>
    Task<OperationResult<ICollection<PrizeType>>> GetWaifuPrizesAsync();
}
