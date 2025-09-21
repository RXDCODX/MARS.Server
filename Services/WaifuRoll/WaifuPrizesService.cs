using MARS.Server.Services.WaifuRoll.helpers;
using MARS.Server.Services.WaifuRoll.Interfaces;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuPrizesService(
    IDbContextFactory<AppDbContext> factory,
    IOptions<ShikimoriClientOptions> shikiOptions,
    WaifuRollEnsurenceService waifuDbHelper
) : ITelegramusService, IWaifuPrizesService
{
    private string ShikimoriSite => shikiOptions.Value.ShikimoriSite;

    public async Task<OperationResult<ICollection<PrizeType>>> GetWaifuPrizesAsync()
    {
        var result = OperationResult<ICollection<PrizeType>>.Bad("Ошибка при получении призов вайфу");

        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var waifus = await dbContext.Waifus.AsNoTracking().Where(e => true).ToListAsync();

            var prizes = new List<PrizeType>();

            foreach (var waifu in waifus)
            {
                // Убеждаемся, что поля аниме и манги заполнены
                var waifuWithTitles = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                prizes.Add(
                    new PrizeType()
                    {
                        Id = waifuWithTitles.ShikiId,
                        Image = ShikimoriSite + "/" + waifuWithTitles.ImageUrl,
                        Text = waifuWithTitles.Name,
                    }
                );
            }

            result = OperationResult<ICollection<PrizeType>>.Ok("Призы вайфу успешно получены", prizes);
        }
        catch (Exception ex)
        {
            result = OperationResult<ICollection<PrizeType>>.Bad($"Ошибка при получении призов вайфу: {ex.Message}");
        }

        return result;
    }
}
