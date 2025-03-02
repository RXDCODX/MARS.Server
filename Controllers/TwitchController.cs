using MARS.Server.Services.Twitch.Management;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

public class TwitchController(TokenService tokenService, ITwitchAPI api, IServer server)
    : Controller
{
    [HttpGet("/twitchtoken")]
    public async Task<IActionResult> Token([FromQuery] string code)
    {
        var adress = server.Features.Get<IServerAddressesFeature>();
        var authToken = await api.Auth.GetAccessTokenFromCodeAsync(
            code,
            api.Settings.Secret,
            adress!.Addresses.FirstOrDefault() + "/twitchauth",
            api.Settings.ClientId
        );

        await tokenService.ApplyNewTokenAsync(
            authToken.AccessToken,
            authToken.RefreshToken,
            authToken.ExpiresIn
        );
        return Ok(authToken);
    }
}
