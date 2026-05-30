using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.WaifuRoll;
using MARS.Server.Services.WaifuRoll.Entitys.Interfaces;
using MARS.Server.Services.WaifuRoll.helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShikimoriSharp.Classes;
using TwitchLib.Api.Helix.Models.Chat;

namespace MARS.Server.Services.Twitch.Rewards._5_AddWife;

public class AddNewWaifu(
    ILogger<AddNewWaifu> logger,
    ITwitchClient client,
    ShikimoriService shikimoriService,
    IOptions<ShikimoriClientOptions> options,
    WaifuRollService waifuRollService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    ITwitchAPI api,
    TokenService tokenService,
    IWaifuRollGuaranteeService guaranteeService,
    WaifuRollEnsurenceService waifuDbHelper,
    AddWife_TwitchReward reward
) : BackgroundService
{
    private readonly ShikimoriClientOptions _options = options.Value;

    private Guid? RewardGuid => reward.TwitchRewardId;
    private const int GuaranteeRolls = 200; // Количество роллов для гаранта

    public bool IsServiceActive { get; set; } = true;

    private async Task AddNewWaifuTwitchEvent(
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

        if (
            RewardGuid.HasValue
            && Guid.Parse(rewardId) == RewardGuid
            && !TwitchExstension.BlackList.Any(t =>
                t.Equals(
                    onMessageReceivedArgs.ChatMessage.Username,
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            await Task.Factory.StartNew(async () =>
            {
                var userName = onMessageReceivedArgs.ChatMessage.DisplayName;
                var userInput = onMessageReceivedArgs.ChatMessage.Message;
                var userId = onMessageReceivedArgs.ChatMessage.UserId;
                var isVip =
                    onMessageReceivedArgs.ChatMessage.UserDetail.IsVip
                    || onMessageReceivedArgs.ChatMessage.UserDetail.IsModerator
                    || onMessageReceivedArgs.ChatMessage.IsBroadcaster;

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

                var operationResult = await waifuRollService.AddNewWaifu(character);

                if (!operationResult.Success)
                {
                    var template = $"@{{user}}, {operationResult.Message}";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await client.SendMessageToMainTwitchAsync(message, logger);
                    return;
                }

                var waifu = operationResult.Data.Waifu;

                if (waifu != null)
                {
                    waifu.IsAdded = true;
                    waifu.ImageUrl = _options.ShikimoriSite + waifu.ImageUrl;

                    // Убеждаемся, что поля аниме и манги заполнены
                    waifu = await waifuDbHelper.EnsureMangaAndAnimeTitleExists(waifu);

                    var color = await api.Helix.Chat.GetUserChatColorAsync([userId]);

                    await hubContext.Clients.All.AddNewWaifu(waifu, userName, color.Data[0]?.Color);

                    if (!isVip)
                    {
                        // Увеличиваем счетчик роллов пользователя
                        await guaranteeService.IncrementRollCountAsync(userId);

                        // Проверяем, выпал ли VIP статус
                        var vipDropped = await guaranteeService.CheckVipDropAsync(userId);

                        if (vipDropped.Data.IsVipDropped)
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

                            // Сбрасываем счетчик роллов после выпадения VIP
                            await guaranteeService.ResetRollCountAsync(userId);
                        }
                        else
                        {
                            // Получаем информацию о гаранте для отображения
                            var guaranteeInfo = await guaranteeService.GetGuaranteeInfoAsync(
                                userId
                            );
                            var rollsUntilGuarantee =
                                GuaranteeRolls - (guaranteeInfo.Data?.RollCount ?? 0);

                            var message =
                                AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                                    userName,
                                    AnswersForTwitchRewards.Answers[Command.AddNewWaifu],
                                    null,
                                    null,
                                    waifu
                                ) + $" Осталось до гаранта: {rollsUntilGuarantee} роллов!";
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsServiceActive)
        {
            client.OnMessageReceived += AddNewWaifuTwitchEvent;
        }

        // Ждем остановки сервиса
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.OnMessageReceived -= AddNewWaifuTwitchEvent;
        await base.StopAsync(cancellationToken);
    }
}
