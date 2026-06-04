using TwitchLib.Api.Helix.Models.Moderation.BanUser;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class VanishCommand(ITwitchAPI twitchApi, TokenService tokenService) : BaseCommand
{
    public override string CommandName => "vanish";
    public override string Description =>
        "Отправляет в таймаут на 1 секунду пользователя, который вызвал команду";
    public override bool IsAdminCommand => false;

    public override Platform[] AvailablePlatforms => [Platform.Twitch];

    public override CommandParameterInfo[] Parameters => [];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось отправить пользователя в таймаут";

        if (parameters.TryGetValue("user", out var userObj) && userObj is TwitchUser user)
        {
            var accessToken = tokenService.Token?.AccessToken;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                try
                {
                    await twitchApi.Helix.Moderation.BanUserAsync(
                        TwitchExstension.ChannelId,
                        TwitchExstension.ChannelId,
                        new BanUserRequest
                        {
                            Duration = 1,
                            Reason = "Vanish",
                            UserId = user.TwitchId,
                        },
                        accessToken
                    );

                    result = $"✅ Пользователь {user.DisplayName} отправлен в таймаут на 1 секунду";
                }
                catch (Exception ex)
                {
                    result = $"❌ Ошибка при отправке в таймаут: {ex.Message}";
                }
            }
            else
            {
                result = "Не найден Twitch access token для выполнения таймаута";
            }
        }
        else
        {
            result = "Не удалось получить пользователя, который вызвал команду";
        }

        return result;
    }
}
