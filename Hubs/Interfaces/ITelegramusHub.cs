using MARS.Server.Services.AutoArts.Entitys;
using MARS.Server.Services.Twitch.Rewards.TwitchScreenParticles.Entitys;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using TwitchLib.Client.Models;

namespace MARS.Server.Hubs.Interfaces;

[SignalRHub("/telegramus", AutoDiscover.MethodsAndParams)]
public interface ITelegramusHub
{
    public Task Alert(MediaDto info);
    public Task Alerts(MediaDto[] info);

    [SignalRMethod]
    public Task UpdateWaifuPrizes(ICollection<PrizeType> prizes);

    [SignalRMethod]
    public Task WaifuRoll(
        Waifu content,
        string displayName,
        Host? waifuHusband,
        string? color = default
    );

    [SignalRMethod]
    public Task AddNewWaifu(Waifu content, string displayName, string? color = default);
    public Task MergeWaifu(
        Waifu content,
        Host host,
        string? avatar = default,
        string? color = default
    );
    public Task FumoFriday(string displayName, string? color = null);
    public Task NewMessage(string id, ChatMessage message);
    public Task DeleteMessage(string id);

    [SignalRMethod]
    public Task Highlite(ChatMessage message, string color, Image faceUrl);

    [SignalRMethod]
    public Task PostTwitchInfo(string clientId, string secret);

    [SignalRMethod]
    public Task MakeScreenParticles(TwitchScreenParticles particles);

    [SignalRMethod]
    public Task MakeScreenEmojisParticles(ChatMessage message);

    [SignalRMethod]
    public Task RandomMem(MediaDto mediaInfo);
}
