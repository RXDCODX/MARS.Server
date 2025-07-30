using System.Text.RegularExpressions;
using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using ShikimoriSharp.Classes;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class AddNewWaifu(
    ILogger<AddNewWaifu> logger,
    ITwitchClient client,
    ShikimoriService shikimoriService,
    IOptions<ShikimoriClientOptions> options,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ITwitchAPI api,
    IHostApplicationLifetime lifetime,
    TokenService tokenService
) : ManagedServiceBase(logger)
{
    private readonly ShikimoriClientOptions _options = options.Value;

    private static Guid RewardGuid => Guid.Parse("a0c9d421-cf76-4f76-9bc6-3cf28da1ffaf");
    private const int VipChance = 5;

    public override string ServiceName => "addnewwaifu";
    public override string DisplayName => "Add New Waifu";
    public override string Description => "Добавление новой вайфу через Twitch";
    public override bool IsServiceActive { get; set; }

    private async void AddNewWaifuTwitchEvent(
        object? sender,
        OnMessageReceivedArgs onMessageReceivedArgs
    )
    {
        var broadcasterId = onMessageReceivedArgs.ChatMessage.RoomId;
        var rewardId = onMessageReceivedArgs.ChatMessage.CustomRewardId;

        if (string.IsNullOrWhiteSpace(rewardId))
        {
            return;
        }

        if (!broadcasterId.Equals(TwitchExstension.ChannelId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Guid.Parse(rewardId) == RewardGuid)
        {
            await Task.Factory.StartNew(async () =>
            {
                var userName = onMessageReceivedArgs.ChatMessage.DisplayName;
                var userInput = onMessageReceivedArgs.ChatMessage.Message;
                var userId = onMessageReceivedArgs.ChatMessage.UserId;
                var isVip = onMessageReceivedArgs.ChatMessage.IsVip;

                var id = await GetShikimoriCharacterIdFromLink(userInput);

                if (id == 0)
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, кривая ссылка! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await client.SendMessageToMainTwitchAsync(message, logger);
                    return;
                }

                FullCharacter? character = await shikimoriService.GetShikiCharacterById(id);

                if (character is null)
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, проблема с ссылкой и получением с неё id персонажа! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await client.SendMessageToMainTwitchAsync(message, logger);
                    return;
                }

                var (waifu, isException) = await waifuRollService.AddNewWaifu(character);

                if (waifu is null && !isException)
                {
                    const string template = "@{user}, такой персонаж уже есть! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await client.SendMessageToMainTwitchAsync(message, logger);
                    return;
                }

                if (waifu != null)
                {
                    waifu.IsAdded = true;
                    waifu.ImageUrl = _options.ShikimoriSite + waifu.ImageUrl;

                    var color = await api.Helix.Chat.GetUserChatColorAsync([userId]);

                    await hubContext.Clients.All.AddNewWaifu(waifu, userName, color.Data[0]?.Color);

                    if (!isVip)
                    {
                        var chance = Random.Shared.Next(0, 101);
                        if (chance >= 100 - VipChance)
                        {
                            if (tokenService.Token == null)
                            {
                                return;
                            }

                            var message =
                                $"@{userName}! Поздравляю, ты получил VIP -статус за добавление персонажей!";
                            await api.Helix.Chat.SendChatAnnouncementAsync(
                                TwitchExstension.ChannelId,
                                TwitchExstension.ChannelId,
                                message,
                                AnnouncementColors.Primary,
                                tokenService.Token.AccessToken
                            );
                            await api.Helix.Channels.AddChannelVIPAsync(
                                TwitchExstension.ChannelId,
                                userId,
                                tokenService.Token.AccessToken
                            );
                        }
                        else
                        {
                            var message =
                                AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                    userName,
                                    AnswersForTwitchRewards.Answers[Command.AddNewWaifu],
                                    null,
                                    null,
                                    waifu
                                )
                                + "Тебе выпало число "
                                + chance
                                + " !";
                            await client.SendMessageToMainTwitchAsync(message);
                        }
                    }
                    else
                    {
                        var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                            userName,
                            AnswersForTwitchRewards.Answers[Command.AddNewWaifu],
                            null,
                            null,
                            waifu
                        );
                        await client.SendMessageToMainTwitchAsync(message);
                    }

                    return;
                }

                const string template3 = "@{user}, не удалось добавить твоего супруга! :-(";
                var resultMessage = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                    userName,
                    template3,
                    null,
                    null,
                    waifu
                );
                await client.SendMessageToMainTwitchAsync(resultMessage, logger);
            });
        }
    }

    private ValueTask<long> GetShikimoriCharacterIdFromLink(string url)
    {
        var regex = new Regex($"{_options.ShikimoriSite}/characters/([a-zA-Z]*\\d+)");

        Match match = regex.Match(url);

        if (!match.Success)
        {
            return ValueTask.FromResult(0L);
        }

        var characterId = match.Groups[1].Value;
        return ValueTask.FromResult(long.Parse(characterId));
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                client.OnMessageReceived += AddNewWaifuTwitchEvent;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        client.OnMessageReceived -= AddNewWaifuTwitchEvent;

        return base.StopAsync(cancellationToken);
    }
}
