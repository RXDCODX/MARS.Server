using MARS.Server.DataBaseContext;
using MARS.Server.Exstensions;
using MARS.Server.Services.Twitch.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Entitys.Subs;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Interfaces;

namespace MARS.Server.Services.Twitch.Rewards._7_Quiz;

public class TwitchTrivia(
    ITwitchClient client,
    IWebHostEnvironment environment,
    ILogger<TwitchTrivia> logger,
    IDbContextFactory<AppDbContext> dbContextFactory
) : ITwitchMiniGame, ITwitchReward
{
    public string Name => "trivia";
    public bool IsReuseRewardForAddMechanic { get; set; } = false;
    public bool IsGameRunning { get; set; }
    public int Cost { get; init; } = 7;

    private const int ChanceToBeSaved = 30;
    internal int CountQuestions;
    internal readonly SemaphoreSlim SemaphoreSlim = new(1);
    internal readonly List<string> NoWaifuHelpUsers = [];

    public readonly int TimeoutBetweenHints = 10;

    internal CancellationTokenSource TokenSource = new();

    internal string FilenameTrivia =>
        Path.Combine(environment.ContentRootPath, "Trivia", "bot_trivia_questions.txt");
    private VictorinaGame? CurrentGame { get; set; }

    public async Task OnChatMessage(string userName, string userId, string message)
    {
        if (!IsGameRunning || userName == TwitchExstension.BotName)
        {
            return;
        }

        message = message.Trim();

        if (CurrentGame != null && CurrentGame.Answer != "")
        {
            try
            {
                Waifu? waifu = null;
                await using AppDbContext context = await dbContextFactory.CreateDbContextAsync();

                var host = await context.Husbands.FindAsync(userId);

                if (host is { IsPrivated: true } && !NoWaifuHelpUsers.Contains(userId))
                {
                    var chance = Random.Shared.Next(0, 101);
                    if (chance < ChanceToBeSaved)
                    {
                        waifu = await context.Waifus.FindAsync(host.WaifuBrideId);
                    }
                    else
                    {
                        NoWaifuHelpUsers.Add(userId);
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
                        $"@{userName} отгадал загаданное слово: {answer}",
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
                        $"@{userName}, поздравляем, ты победил! Твой супруг ({waifu.Name}) шепнул(-а) тебе на ушко загаданное слово: {answer}",
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
    }

    public int GetGameCost()
    {
        return Cost;
    }

    public async Task GameStart(
        string userName,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            CountQuestions = (
                await File.ReadAllLinesAsync(FilenameTrivia, cancellationToken)
            ).Length;
            TokenSource = new CancellationTokenSource();
            var qwe = new VictorinaGame(logger, client, this);
            CurrentGame = qwe;
            IsGameRunning = true;
            await qwe.MainThread();
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
            IsGameRunning = false;
        }
    }

    public Task CancelAsync()
    {
        try
        {
            CurrentGame!.Active = false;
        }
        catch
        {
            // ignore
        }
        finally
        {
            IsGameRunning = false;
            TokenSource.Cancel();
        }

        return Task.CompletedTask;
    }

    public Task<bool> OnRewardRedemption(string userName, string userId, int cost)
    {
        return Task.FromResult(false);
    }
}
