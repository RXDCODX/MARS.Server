using MARS.Server.Services.Twitch;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

public class TwitchController : Controller
{
    private readonly ITwitchAPI _api;
    private readonly UserAuthHelper _helper;
    private readonly IServer _server;

    public TwitchController(UserAuthHelper helper, ITwitchAPI api, IServer server)
    {
        _helper = helper;
        _api = api;
        _server = server;
    }

    [HttpGet("/twitchtoken")]
    public async Task<IActionResult> Token([FromQuery] string code)
    {
        var adress = _server.Features.Get<IServerAddressesFeature>();
        var authToken = await _api.Auth.GetAccessTokenFromCodeAsync(
            code,
            _api.Settings.Secret,
            adress!.Addresses.FirstOrDefault() + "/twitchauth",
            _api.Settings.ClientId
        );

        _helper.ApplyNewTokenFromAuth(
            authToken.AccessToken,
            authToken.RefreshToken,
            authToken.ExpiresIn
        );
        return Ok(authToken);
    }
}
