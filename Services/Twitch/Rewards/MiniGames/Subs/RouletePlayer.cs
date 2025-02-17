namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;

public class RouletePlayer
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string Name { get; init; }
    public bool IsAlive { get; set; } = true;
    public string TwitchId { get; init; }
}
