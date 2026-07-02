namespace MARS.Server.Services.Twitch.Entitys;

public class UpdateTwitchUserRequest
{
    public string? UserLogin { get; set; }
    public string? DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ChatColor { get; set; }
    public bool? IsModerator { get; set; }
    public bool? IsVip { get; set; }
    public bool? IsInBlockList { get; set; }
    public string? AliasNickname { get; set; }
}
