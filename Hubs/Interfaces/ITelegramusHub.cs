using MARS.Server.Services.AutoArts.Entitys;
using SignalRSwaggerGen.Attributes;
using TwitchLib.Client.Models;

namespace MARS.Server.Hubs.Interfaces;

[SignalRHub]
public interface ITelegramusHub
{
    public Task Alert(MediaDto info);
    public Task UpdateWaifuPrizes(ICollection<PrizeType> prizes);
    public Task WaifuRoll(Waifu content, string? displayName, string color = "white");
    public Task AddNewWaifu(Waifu content, string? displayName, string color = "white");
    public Task MergeWaifu(Waifu content, string? displayName, string color = "white");
    public Task FumoFriday(string displayName, string? color = null);
    public Task NewMessage(string id, ChatMessage message);
    public Task DeleteMessage(string id);
    public Task Highlite(ChatMessage message, string color, Image faceUrl);
    public Task PostTwitchInfo(string clientId, string secret);
    public Task TwitchMsg(string msg);
}
