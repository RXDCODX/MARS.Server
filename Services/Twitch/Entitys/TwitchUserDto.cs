namespace MARS.Server.Services.Twitch.Entitys;

public class TwitchUserDto
{
    public required string TwitchId { get; set; }
    public required string UserLogin { get; set; }
    public required string DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ChatColor { get; set; }
    public bool IsModerator { get; set; }
    public bool IsVip { get; set; }
    public bool IsBroadcaster { get; set; }
    public bool IsInBlockList { get; set; }
    public string? AliasNickname { get; set; }
    public DateTime? FollowedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
}
