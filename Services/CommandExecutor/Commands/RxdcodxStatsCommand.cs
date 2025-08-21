using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.TwitchFollowers;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для получения статистики канала rxdcodx
/// </summary>
public class RxdcodxStatsCommand : BaseCommand
{
    private readonly IRxdcodxViewersService _viewersService;

    public RxdcodxStatsCommand(IRxdcodxViewersService viewersService)
    {
        _viewersService = viewersService;
    }

    public override string CommandName => "rxdcodxstats";
    public override string Description => "Показать статистику канала rxdcodx";
    public override bool IsAdminCommand => false;
    public override List<string> Aliases => ["rxstats", "rxdcodx"];

    public override bool IsAvailableOnPlatform(Platform platform) => platform == Platform.Twitch;

    public override async Task<string> ExecuteAsync(string[] args, string userId, string username)
    {
        try
        {
            var followersCount = await _viewersService.GetFollowersCount();
            var vipsCount = await _viewersService.GetVIPsCount();
            var moderatorsCount = await _viewersService.GetModeratorsCount();

            if (followersCount == 0 && vipsCount == 0 && moderatorsCount == 0)
            {
                return "Не удалось получить статистику канала rxdcodx. Возможно, токен недоступен.";
            }

            return $"📊 Статистика канала rxdcodx: 👥 Фоловеры: {followersCount} | ⭐ VIP: {vipsCount} | 🛡️ Модераторы: {moderatorsCount}";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка при получении статистики: {ex.Message}";
        }
    }
}
