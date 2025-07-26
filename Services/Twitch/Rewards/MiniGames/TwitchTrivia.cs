using MARS.Server.Services.ServiceManager;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchTrivia(
    ITwitchClient client,
    IWebHostEnvironment environment,
    ILogger<TwitchTrivia> logger,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHostApplicationLifetime applicationLifetime,
    EventSubWebsocketClient wsClient
) : ManagedServiceBase(logger), ITwitchMiniGame
{
    public override string ServiceName => "twitchtrivia";
    public override string DisplayName => "Twitch Trivia";
    public override string Description => "Мини-игра Trivia на Twitch";
    public override bool IsServiceActive { get; set; }
    public bool IsReuseRewardForAddMechanic { get; set; } = false;
    public bool IsGameRunning { get; set; }
    public string CommandForStop { get; set; } = "!викторинастоп";

    private const int CostOfAlert = 7;
    private const int ChanceToBeSaved = 30;
    internal int CountQuestions;
    internal readonly SemaphoreSlim SemaphoreSlim = new(1);
    internal readonly List<string> NoWaifuHelpUsers = [];

    public readonly int TimeoutBetweenHints = 10;

    internal CancellationTokenSource TokenSource = null!;

    internal string FilenameTrivia =>
        Path.Combine(environment.ContentRootPath, "Trivia", "bot_trivia_questions.txt");
    private VictorinaGame? CurrentGame { get; set; }

    private async void NewMessage(object? sender, OnMessageReceivedArgs onMessageReceivedArgs)
    {
        await Task.Run(
            async () =>
            {
                var name = onMessageReceivedArgs.ChatMessage.Username;
                var message = onMessageReceivedArgs.ChatMessage.Message.Trim();
                var id = onMessageReceivedArgs.ChatMessage.UserId;

                if (name == TwitchExstension.BotName || !IsServiceActive)
                {
                    return;
                }

                //Стоп - слово
                if (
                    message.Equals(CommandForStop, StringComparison.OrdinalIgnoreCase)
                    && name.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase)
                )
                {
                    if (CurrentGame != null)
                    {
                        CurrentGame.Active = false;
                        CurrentGame = null;
                        await client.SendMessageToMainTwitchAsync("Остановка тривии", logger);
                    }
                    else
                    {
                        await client.SendMessageToMainTwitchAsync(
                            "Тривия не была запущена",
                            logger
                        );
                    }

                    IsGameRunning = false;
                    return;
                }

                //травия ответы
                if (CurrentGame != null && CurrentGame.Answer != "")
                {
                    try
                    {
                        Waifu? waifu = null;
                        await using AppDbContext context =
                            await dbContextFactory.CreateDbContextAsync();

                        var host = await context.Hosts.FindAsync(id);

                        if (host is { IsPrivated: true } && !NoWaifuHelpUsers.Contains(id))
                        {
                            var chance = Random.Shared.Next(0, 101);
                            if (chance < ChanceToBeSaved)
                            {
                                waifu = await context.Waifus.FindAsync(host.WaifuBrideId);
                            }
                            else
                            {
                                NoWaifuHelpUsers.Add(id);
                            }
                        }

                        await SemaphoreSlim.WaitAsync(TokenSource.Token);
                        if (
                            message.Equals(CurrentGame.Answer, StringComparison.OrdinalIgnoreCase)
                            && !CurrentGame.AllLettersShowed
                        )
                        {
                            CurrentGame.AllLettersShowed = true;
                            var answer = CurrentGame.Answer;
                            CurrentGame.Answer = "";
                            await client.SendMessageToMainTwitchAsync(
                                $"@{name} отгадал загаданное слово: {answer}",
                                logger
                            );
                            IsGameRunning = false;
                            NoWaifuHelpUsers.Clear();
                        }

                        if (!CurrentGame.AllLettersShowed && waifu != null)
                        {
                            CurrentGame.AllLettersShowed = true;
                            var answer = CurrentGame.Answer;
                            CurrentGame.Answer = "";
                            await client.SendMessageToMainTwitchAsync(
                                $"@{name}, поздравляем, ты победил! Твой супруг ({waifu.Name}) шепнул(-а) тебе на ушко загаданное слово: {answer}",
                                logger
                            );
                            IsGameRunning = false;
                            NoWaifuHelpUsers.Clear();
                        }

                        SemaphoreSlim.Release();
                    }
                    catch (Exception ex)
                    {
                        logger.LogException(ex);
                    }
                }
            },
            TokenSource.Token
        );
    }

    public int GetGameCost()
    {
        return CostOfAlert;
    }

    public Task GameStart(string userName, string userId)
    {
        if (IsServiceActive)
        {
            try
            {
                var qwe = new VictorinaGame(logger, client, this);
                CurrentGame = qwe;
                qwe.MainThread();
                IsGameRunning = false;
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        return Task.CompletedTask;
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CountQuestions = (await File.ReadAllLinesAsync(FilenameTrivia, cancellationToken)).Length;
        TokenSource = new CancellationTokenSource();
        IsServiceActive = true;
        await base.StartAsync(cancellationToken);

        if (IsServiceActive)
        {
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                client.OnMessageReceived += NewMessage;
                wsClient.StreamOffline += WsClientOnStreamOffline;
            });
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken = default)
    {
        TokenSource = new CancellationTokenSource();
        IsServiceActive = false;
        CountQuestions = 0;
        client.OnMessageReceived -= NewMessage;
        wsClient.StreamOffline -= WsClientOnStreamOffline;
        return base.StopAsync(cancellationToken);
    }

    private Task WsClientOnStreamOffline(object sender, StreamOfflineArgs args)
    {
        return StopAsync(applicationLifetime.ApplicationStopping);
    }
}
