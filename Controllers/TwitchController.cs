using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace MARS.Server.Controllers;

public class TwitchController(
    TokenService tokenService,
    ITwitchAPI api,
    IServer server,
    ILogger<TwitchController> logger
) : Controller
{
    [HttpGet("/" + nameof(TwitchUserAuth))]
    public async Task<ActionResult<OperationResult<object?>>> TwitchUserAuth(
        [FromQuery] string code
    )
    {
        ActionResult<OperationResult<object?>> result = null!;

        try
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var adress = server.Features.Get<IServerAddressesFeature>();
                var authToken = await api.Auth.GetAccessTokenFromCodeAsync(
                    code,
                    api.Settings.Secret,
                    adress!.Addresses.First() + "/" + nameof(TwitchUserAuth),
                    api.Settings.ClientId
                );

                await tokenService.ApplyNewTokenAsync(
                    authToken.AccessToken,
                    authToken.RefreshToken,
                    authToken.ExpiresIn
                );

                result = Ok(
                    OperationResult<object?>.Ok("Авторизация успешно выполнена", authToken)
                );
            }
            else
            {
                result = Ok(OperationResult<object?>.Bad("Код авторизации не предоставлен", null));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при авторизации Twitch пользователя");
            result = Ok(OperationResult<object?>.Bad("Ошибка при авторизации", null));
        }

        return result;
    }
}
