using MARS.Server.Services.Twitch.Management;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

public class TwitchController(TokenService tokenService, ITwitchAPI api, IServer server)
    : Controller
{
    [HttpGet("/" + nameof(TwitchUserAuth))]
    public async Task<ActionResult<object>> TwitchUserAuth([FromQuery] string code)
    {
        ActionResult<object> result = Ok(new { message = "No code provided" });
        
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
            result = Ok(authToken);
        }
        
        return result;
    }
}
