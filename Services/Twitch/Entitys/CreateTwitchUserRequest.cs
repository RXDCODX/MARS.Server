namespace MARS.Server.Services.Twitch.Entitys;

public class CreateTwitchUserRequest
{
    public required string TwitchId { get; set; }
    public required string UserLogin { get; set; }
    public required string DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ChatColor { get; set; }
    public bool IsModerator { get; set; }
    public bool IsVip { get; set; }
    public bool IsInBlockList { get; set; }
    public string? AliasNickname { get; set; }
}
