using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace MARS.Server.Services.Twitch.Entitys;

public class GaoAlertDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public User? TwitchUser { get; set; }
    public bool IsJustText { get; set; }
    public string? JustText { get; set; }
}
