using MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;
using TwitchLib.Client.Events;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TwitchTrivia : BackgroundService
{
    private const int CostOfAlert = 7;
    private const string CommandForStop = "!викторинастоп";
    private const int ChanceToBeSaved = 30;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TwitchTrivia> _logger;
    internal readonly int CountQuestions;
    internal readonly object Locker = new();
    internal readonly SemaphoreSlim SemaphoreSlim = new(1);

    public readonly int TimeoutBetweenHints = 10;
    internal bool IsGameActive = false;

    internal CancellationTokenSource TokenSource = null!;

    private readonly IHostApplicationLifetime _applicationLifetime;
    private bool IsAppActive { get; set; } = false;

    public TwitchTrivia(
        ITwitchClient client,
        IWebHostEnvironment environment,
        ILogger<TwitchTrivia> logger,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _client = client;
        _environment = environment;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _applicationLifetime = applicationLifetime;
        CountQuestions = File.ReadAllLines(FilenameTrivia).Length;

        _client.OnMessageReceived += NewMessage;
        UserAuthHelper.WsClient.ChannelPointsCustomRewardRedemptionAdd += NewAlert;
        UserAuthHelper.WsClient.StreamOffline += Closing;

        CountQuestions = File.ReadAllLines(FilenameTrivia).Length;
        applicationLifetime.ApplicationStarted.Register(() => IsAppActive = true);
    }

    internal string FilenameTrivia =>
        Path.Combine(_environment.ContentRootPath, "Trivia", "bot_trivia_questions.txt");
    private VictorinaGame? CurrentGame { get; set; }

    private bool IsStop { get; set; } = true;

    private Task Init()
    {
        TokenSource = new CancellationTokenSource();
        IsStop = false;

        return Task.CompletedTask;
    }

    private Task Closing(object sender, StreamOfflineArgs args)
    {
        if (!IsStop)
        {
            if (CurrentGame != null)
            {
                CurrentGame.Active = false;
            }
        }

        IsStop = true;

        return Task.CompletedTask;
    }

    private async void NewMessage(object? sender, OnMessageReceivedArgs onMessageReceivedArgs)
    {
        if (!IsAppActive)
        {
            return;
        }

        await Task.Run(
            async () =>
            {
                var name = onMessageReceivedArgs.ChatMessage.Username;
                var message = onMessageReceivedArgs.ChatMessage.Message;
                var id = onMessageReceivedArgs.ChatMessage.UserId;

                if (name == TwitchExstension.BotName || IsStop)
                {
                    return;
                }

                //Стоп - слово
                if (message == CommandForStop && name == "")
                {
                    if (CurrentGame != null)
                    {
                        CurrentGame.Active = false;
                        await _client.SendMessageToPyrokxnezxzAsync("Остановка тривии", _logger);
                    }
                    else
                    {
                        await _client.SendMessageToPyrokxnezxzAsync(
                            "Тривия не была запущена",
                            _logger
                        );
                    }

                    return;
                }

                //травия ответы
                if (
                    CurrentGame != null
                    && message.Equals(CurrentGame.Answer, StringComparison.OrdinalIgnoreCase)
                    && CurrentGame.Answer != ""
                )
                {
                    try
                    {
                        Waifu? waifu = null;
                        await using AppDbContext context =
                            await _dbContextFactory.CreateDbContextAsync();

                        var host = await context.Hosts.FindAsync(id);

                        if (
                            host is { IsPrivated: true }
                            && message.Length == CurrentGame.Answer.Length
                        )
                        {
                            var isTextSimmetric =
                                message.Length == CurrentGame.Answer.Length
                                && message.Where((t, i) => CurrentGame.Answer[i].Equals(t)).Any();

                            if (isTextSimmetric)
                            {
                                var chance = Random.Shared.Next(0, 101);
                                if (chance < ChanceToBeSaved)
                                {
                                    waifu = await context.Waifus.FindAsync(host.WaifuBrideId);
                                }
                            }
                        }

                        await SemaphoreSlim.WaitAsync(TokenSource.Token);
                        if (!CurrentGame.AllLettersShowed && waifu != null)
                        {
                            CurrentGame.AllLettersShowed = true;
                            var answer = CurrentGame.Answer;
                            CurrentGame.Answer = "";
                            await _client.SendMessageToPyrokxnezxzAsync(
                                $"@{name}, твой супруг ({waifu.Name}) шепнул(-а) на ушко загаданное слово: {answer}",
                                _logger
                            );
                        }
                        else if (!CurrentGame.AllLettersShowed)
                        {
                            CurrentGame.AllLettersShowed = true;
                            var answer = CurrentGame.Answer;
                            CurrentGame.Answer = "";
                            await _client.SendMessageToPyrokxnezxzAsync(
                                $"@{name} отгадал загаданное слово: {answer}",
                                _logger
                            );
                        }

                        SemaphoreSlim.Release();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex);
                    }
                }
            },
            TokenSource.Token
        );
    }

    private Task NewAlert(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        if (args.Notification.Payload.Event.Reward.Cost == CostOfAlert)
        {
            if (!IsGameActive)
            {
                if (!IsStop)
                {
                    try
                    {
                        var qwe = new VictorinaGame(_logger, _client, this);
                        CurrentGame = qwe;
                        qwe.MainThread();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogException(ex);
                    }
                }
            }
            else
            {
                const string answer = "@{0}, викторина уже запущена!";
                return _client.SendMessageToPyrokxnezxzAsync(
                    string.Format(answer, args.Notification.Payload.Event.UserName),
                    _logger
                );
            }
        }

        return Task.CompletedTask;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Init();
    }
}
