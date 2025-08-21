using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.TwitchFollowers;

namespace MARS.Server.Services.CommandExecutor.Commands;

/// <summary>
/// Команда для проверки статуса пользователя на канале rxdcodx
/// </summary>
public class UserStatusCommand : BaseCommand
{
    private readonly IRxdcodxViewersService _viewersService;

    public UserStatusCommand(IRxdcodxViewersService viewersService)
    {
        _viewersService = viewersService;
    }

    public override string CommandName => "userstatus";
    public override string Description => "Проверить статус пользователя на канале rxdcodx";
    public override bool IsAdminCommand => false;
    public override List<string> Aliases => ["status", "checkuser", "userinfo"];

    public override bool IsAvailableOnPlatform(Platform platform) => platform == Platform.Twitch;

    public override async Task<string> ExecuteAsync(string[] args, string userId, string username)
    {
        try
        {
            // Если аргументы не указаны, проверяем статус пользователя, который вызвал команду
            var targetUserId = args.Length > 0 ? args[0] : userId;
            var targetUsername = args.Length > 0 ? args[0] : username;

            var isFollower = await _viewersService.IsUserFollower(targetUserId);
            var isVIP = await _viewersService.IsUserVIP(targetUserId);
            var isModerator = await _viewersService.IsUserModerator(targetUserId);

            if (!isFollower && !isVIP && !isModerator)
            {
                return $"👤 {targetUsername} - обычный зритель канала rxdcodx";
            }

            var status = isModerator ? "🛡️ Модератор" : isVIP ? "⭐ VIP" : "👥 Фоловер";
            return $"👤 {targetUsername} - {status} канала rxdcodx";
        }
        catch (Exception ex)
        {
            return $"❌ Ошибка при проверке статуса: {ex.Message}";
        }
    }
}
