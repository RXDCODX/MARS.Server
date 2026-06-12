using System.Threading;
using System.Threading.Tasks;

namespace MARS.Server.Services.Twitch.Entitys.Interfaces;

public interface ITwitchMiniGame
{
    public bool IsReuseRewardForAddMechanic { get; set; }
    public bool IsGameRunning { get; set; }
    public string Name { get; }
    public int GetGameCost();
    public Task GameStart(
        string userName,
        string userId,
        CancellationToken cancellationToken = default
    );
    public Task CancelAsync();
    public Task OnChatMessage(string userName, string userId, string message);
    public Task<bool> OnRewardRedemption(string userName, string userId, int cost);
}
