using System.Text.RegularExpressions;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class AddNewWaifu : BackgroundService
{
    private readonly ITwitchAPI _api;
    private readonly ITwitchClient _client;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<AddNewWaifu> _logger;
    private readonly ShikimoriClientOptions _options;
    private readonly ShikimoriService _shikimoriService;
    private readonly WaifuRollService _waifuRollService;
    private readonly TokenService _tokenService;

    private static Guid RewardGuid => Guid.Parse("a0c9d421-cf76-4f76-9bc6-3cf28da1ffaf");
    private const int VipChance = 5;

    public AddNewWaifu(
        ILogger<AddNewWaifu> logger,
        ITwitchClient client,
        ShikimoriService shikimoriService,
        IOptions<ShikimoriClientOptions> options,
        WaifuRollService waifuRollService,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        ITwitchAPI api,
        IHostApplicationLifetime lifetime,
        TokenService tokenService
    )
    {
        _logger = logger;
        _client = client;
        _shikimoriService = shikimoriService;
        _waifuRollService = waifuRollService;
        _hubContext = hubContext;

        _api = api;
        _tokenService = tokenService;
        _options = options.Value;

        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += AddNewWaifuTwitchEvent;
        });
    }

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

                if (string.IsNullOrWhiteSpace(id))
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, кривая ссылка! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await _client.SendMessageToMainTwitchAsync(message, _logger);
                    return;
                }

                ShikiCharacter? character = await _shikimoriService.GetShikiCharacterById(id);

                if (character is null)
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, проблема с ссылкой и получением с неё id персонажа! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await _client.SendMessageToMainTwitchAsync(message, _logger);
                    return;
                }

                var (waifu, isException) = await _waifuRollService.AddNewWaifu(character);

                if (waifu is null && !isException)
                {
                    const string template = "@{user}, такой персонаж уже есть! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        userName,
                        template
                    );

                    await _client.SendMessageToMainTwitchAsync(message, _logger);
                    return;
                }

                if (waifu != null)
                {
                    waifu.IsAdded = true;
                    waifu.ImageUrl = _options.ShikimoriSite + waifu.ImageUrl;

                    var color = await _api.Helix.Chat.GetUserChatColorAsync([userId]);

                    await _hubContext.Clients.All.AddNewWaifu(
                        waifu,
                        userName,
                        color.Data[0]?.Color
                    );

                    if (!isVip)
                    {
                        var chance = Random.Shared.Next(0, 101);
                        if (chance >= 100 - VipChance)
                        {
                            if (_tokenService.Token == null)
                            {
                                return;
                            }

                            var message =
                                $"@{userName}! Поздравляю, ты получил VIP -статус за добавление персонажей!";
                            await _api.Helix.Chat.SendChatAnnouncementAsync(
                                TwitchExstension.ChannelId,
                                TwitchExstension.ChannelId,
                                message,
                                AnnouncementColors.Primary,
                                _tokenService.Token.AccessToken
                            );
                            await _api.Helix.Channels.AddChannelVIPAsync(
                                TwitchExstension.ChannelId,
                                userId,
                                _tokenService.Token.AccessToken
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
                            await _client.SendMessageToMainTwitchAsync(message);
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
                        await _client.SendMessageToMainTwitchAsync(message);
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
                await _client.SendMessageToMainTwitchAsync(resultMessage, _logger);
            });
        }
    }

    private ValueTask<string> GetShikimoriCharacterIdFromLink(string url)
    {
        var regex = new Regex($"{_options.ShikimoriSite}/characters/([a-zA-Z]*\\d+)");

        Match match = regex.Match(url);

        if (match.Success)
        {
            var characterId = match.Groups[1].Value;
            return ValueTask.FromResult(characterId);
        }

        return ValueTask.FromResult(string.Empty);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
