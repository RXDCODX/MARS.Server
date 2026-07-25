using MARS.Server.Hubs.Models.AudioQuiz;
using MARS.Server.Services.AutoArts_OBSOLETE.Entitys;
using MARS.Server.Services.PyroAlerts.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.WaifuRoll.Entitys;
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
    public Task WaifuRoll(Waifu content, Husband? waifuHusband);

    [SignalRMethod]
    public Task AddNewWaifu(Waifu content, TwitchUser twitchUser);
    public Task MergeWaifu(Waifu content, Husband husband);

    [SignalRMethod]
    public Task ShowCurrentWife(Waifu content, Husband husband);
    public Task FumoFriday(TwitchUser twitchUser);
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
    public Task AudioQuizStart(AudioQuizRoundDto round);

    [SignalRMethod]
    public Task AudioQuizStop();

    [SignalRMethod]
    public Task FumoRoll(
        Fumo fumo,
        TwitchUser twitchUser,
        int collectedCount = 0,
        int totalCount = 0
    );

    [SignalRMethod]
    public Task UpdateFumoPrizes(ICollection<FumoPrizeType> prizes);

    [SignalRMethod]
    public Task FrogRoll(Frog frog, TwitchUser twitchUser);

    [SignalRMethod]
    public Task UpdateFrogPrizes(ICollection<FrogPrizeType> prizes);

    [SignalRMethod]
    public Task MikuRoll(
        MikuModule mikuModule,
        TwitchUser twitchUser,
        int collectedCount = 0,
        int totalCount = 0
    );

    [SignalRMethod]
    public Task UpdateMikuPrizes(ICollection<MikuPrizeType> prizes);
}
