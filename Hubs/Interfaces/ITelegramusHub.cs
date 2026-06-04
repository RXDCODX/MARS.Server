using MARS.Server.Hubs.Models.AudioQuiz;
using MARS.Server.Services.AutoArts_OBSOLETE.Entitys;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;
using TwitchLib.Client.Models;

namespace MARS.Server.Hubs.Interfaces;

[SignalRHub("/hubs/telegramus", AutoDiscover.MethodsAndParams)]
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
        string? color = null
    );

    [SignalRMethod]
    public Task AddNewWaifu(Waifu content, string displayName, string? color = null);
    public Task MergeWaifu(Waifu content, Host host, string? avatar = null, string? color = null);
    public Task FumoFriday(string displayName, string? color = null);
    public Task NewMessage(string id, ChatMessage message);
    public Task DeleteMessage(string id);

    [SignalRMethod]
    public Task Highlite(ChatMessage message, string color, AutoArtImage faceUrl);

    [SignalRMethod]
    public Task PostTwitchInfo(string clientId, string secret);

    [SignalRMethod]
    public Task MakeScreenParticles(TwitchScreenParticles particles);

    [SignalRMethod]
    public Task MakeScreenEmojisParticles(ChatMessage message);

    [SignalRMethod]
    public Task RandomMem(MediaDto mediaInfo);

    [SignalRMethod]
    public Task AutoMessage(string message);

    [SignalRMethod]
    public Task Adhd(int seconds);

    [SignalRMethod]
    public Task Explosion();

    [SignalRMethod]
    public Task LeroyAlert();

    [SignalRMethod]
    Task GaoAlert(GaoAlertDto gaoAlert);

    [SignalRMethod]
    Task Credits();

    [SignalRMethod]
    Task MichaelJackson();

    [SignalRMethod]
    Task MikuMonday(MikuMondayDto mikuMondayData);

    [SignalRMethod]
    Task MikuMikuBeam(List<TwitchUser> users);

    [SignalRMethod]
    Task PhonkEdit();

    [SignalRMethod]
    Task TikTokEdit(Guid guid, string text);

    [SignalRMethod]
    Task AllRefund(TwitchUser user);

    [SignalRMethod]
    Task AudioQuizStart(AudioQuizRoundDto round);

    [SignalRMethod]
    Task AudioQuizStop();
}
