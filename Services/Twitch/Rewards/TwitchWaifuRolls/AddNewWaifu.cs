using System.Text.RegularExpressions;
using MARS.Server.Services.Shikimori;
using MARS.Server.Services.Shikimori.Entitys;
using MARS.Server.Services.WaifuRoll;
using TwitchLib.Api.Helix.Models.Chat;

namespace MARS.Server.Services.Twitch.Rewards.TwitchWaifuRolls;

public class AddNewWaifu : BackgroundService
{
    private readonly ITwitchAPI _api;
    private readonly ITwitchClient _client;
    private readonly UserAuthHelper _helper;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly ILogger<AddNewWaifu> _logger;
    private readonly ShikimoriClientOptions _options;
    private readonly ShikimoriService _shikimoriService;
    private readonly WaifuRollService _waifuRollService;

    public AddNewWaifu(
        ILogger<AddNewWaifu> logger,
        ITwitchClient client,
        ShikimoriService shikimoriService,
        IOptions<ShikimoriClientOptions> options,
        WaifuRollService waifuRollService,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext,
        ITwitchAPI api,
        UserAuthHelper helper,
        IHostApplicationLifetime lifetime
    )
    {
        _logger = logger;
        _client = client;
        _shikimoriService = shikimoriService;
        _waifuRollService = waifuRollService;
        _hubContext = hubContext;

        _api = api;
        _helper = helper;
        _options = options.Value;

        lifetime.ApplicationStarted.Register(() =>
        {
            UserAuthHelper.WsClient.ChannelPointsCustomRewardRedemptionAdd +=
                AddNewWaifuTwitchEvent;
        });
    }

    private async Task AddNewWaifuTwitchEvent(
        object sender,
        ChannelPointsCustomRewardRedemptionArgs args
    )
    {
        var twEvent = args.Notification.Payload.Event;
        if (
            twEvent.BroadcasterUserId.Equals(
                TwitchExstension.ChannelId,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            if (twEvent.Reward.Cost == 5)
            {
                var id = await GetShikimoriCharacterIdFromLink(twEvent.UserInput);

                if (string.IsNullOrWhiteSpace(id))
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, кривая ссылка! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        template
                    );

                    await _client.SendMessageToPyrokxnezxzAsync(message, _logger);
                    return;
                }

                ShikiCharacter? character = await _shikimoriService.GetShikiCharacterById(id);

                if (character is null)
                {
                    const string template =
                        "@{user}, не удалось добавить твоего супруга, проблема с ссылкой и получением с неё id персонажа! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        template
                    );

                    await _client.SendMessageToPyrokxnezxzAsync(message, _logger);
                    return;
                }

                (Waifu? waifu, var isException) = await _waifuRollService.AddNewWaifu(character);

                if (waifu is null && !isException)
                {
                    const string template = "@{user}, такой персонаж уже есть! :-(";
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        template
                    );

                    await _client.SendMessageToPyrokxnezxzAsync(message, _logger);
                    return;
                }

                if (waifu != null)
                {
                    var message = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                        twEvent.UserName,
                        AnswersForTwitchRewards.Answers[Command.AddNewWaifu],
                        null,
                        null,
                        waifu
                    );

                    waifu.IsAdded = true;

                    await _hubContext.Clients.All.AddNewWaifu(waifu, twEvent.UserName);
                    await _client.SendMessageToPyrokxnezxzAsync(message, _logger);

                    var chance = Random.Shared.Next(0, 101);
                    if (chance < 3)
                    {
                        if (_helper.Token != null)
                        {
                            message =
                                $"@{twEvent.UserName}! Поздравляю, ты получил VIP -статус за добавление персонажей!";
                            await _api.Helix.Channels.AddChannelVIPAsync(
                                TwitchExstension.ChannelId,
                                args.Notification.Payload.Event.UserId,
                                _helper.Token.AccessToken
                            );
                            await _api.Helix.Chat.SendChatAnnouncementAsync(
                                TwitchExstension.ChannelId,
                                TwitchExstension.ChannelId,
                                message,
                                AnnouncementColors.Primary,
                                _helper.Token.AccessToken
                            );
                        }
                    }

                    return;
                }

                const string template3 = "@{user}, не удалось добавить твоего супруга! :-(";
                var resultMessage = AnswersForTwitchRewards.ReplaceKeywordsInAnswer(
                    twEvent.UserName,
                    template3,
                    null,
                    null,
                    waifu
                );
                await _client.SendMessageToPyrokxnezxzAsync(resultMessage, _logger);
            }
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
