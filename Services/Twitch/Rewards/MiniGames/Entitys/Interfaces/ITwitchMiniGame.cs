namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;

public interface ITwitchMiniGame
{
    public bool IsReuseRewardForAddMechanic { get; set; }
    public bool IsGameRunning { get; set; }
    public int GetGameCost();
    public Task GameStart(string userName, string userId);
}
