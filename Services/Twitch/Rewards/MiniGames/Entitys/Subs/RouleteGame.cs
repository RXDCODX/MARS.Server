using TwitchLib.Client.Extensions;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;

public class RouleteGame(
    List<RouletePlayer> players,
    GameType type,
    CancellationToken token,
    ITwitchClient client,
    ILogger<TwitchRussianRoulete> logger,
    IDbContextFactory<AppDbContext> factory,
    TwitchRussianRoulete roulette
)
{
    private const int ChanceToBeSaved = 40;
    private List<RouletePlayer> Players { get; } = players.ToList();
    private GameType Type { get; } = type;
    private readonly List<string> _noWaifuHelpUsers = [];

    public async Task RussianRoulette()
    {
        var numPlayers = Players.Count;
        var roundNum = 1;
        var random = new Random();

        if (numPlayers == 1)
        {
            await AloneRoulette(Players[0].Name, token);
            roulette.IsGameRunning = false;
            return;
        }

        if (numPlayers == 2)
        {
            var namesForMinigame = string.Join(", ", Players.Select(player => player.Name));
            await client.SendMessageToMainTwitchAsync(
                $"Играется рулетка на двоих! Играют: {namesForMinigame}",
                logger
            );
        }

        while (Players.Count(e => e.IsAlive) > 1 && !token.IsCancellationRequested)
        {
            var alivePlayers = Players.Where(player => player.IsAlive).ToList();
            if (Type != GameType.MiniGame)
            {
                var alivePlayersNames = string.Join(
                    ", ",
                    alivePlayers.Select(player => player.Name)
                );
                await client.SendMessageToMainTwitchAsync(
                    $"Русская рулетка - раунд {roundNum}! Играют: {alivePlayersNames}",
                    logger
                );
            }

            var index = random.Next(alivePlayers.Count(e => e.IsAlive));
            await Task.Delay(1000, token);
            RouletePlayer shotPlayer = alivePlayers[index];

            var isSaved = await TryToSavePlayer(shotPlayer);

            if (isSaved)
            {
                await using AppDbContext context =
                    await factory.CreateDbContextAsync(token) ?? throw new Exception("еблан?");
                Host host =
                    context.Hosts.Find(shotPlayer.TwitchId)
                    ?? throw new NullReferenceException(
                        "Обращение к спассеному host'у который был спасен"
                    );
                Waifu waifu =
                    await context.Waifus.FirstOrDefaultAsync(
                        e => e.ShikiId == host.WaifuBrideId,
                        token
                    ) ?? throw new NullReferenceException("не найдена зарегестрированная жена");
                await client.SendMessageToMainTwitchAsync(
                    $"@{shotPlayer.Name}, твой супруг - {waifu.Name} спас тебя от неминуемой гибели!",
                    logger
                );
            }
            else
            {
                await client.SendMessageToMainTwitchAsync(
                    StaticContent.PlayerEliminated(shotPlayer.Name),
                    logger
                );
                shotPlayer.IsAlive = false;
            }

            await Task.Delay(2000, token);

            roundNum++;
        }

        RouletePlayer winner = Players.First(e => e.IsAlive);
        if (Type == GameType.MiniGame)
        {
            await client.SendMessageToMainTwitchAsync(
                $"Победитель: {winner.Name}. {StaticContent.GetMiniHistory(winner.Name)}",
                logger
            );
        }
        else
        {
            await client.SendMessageToMainTwitchAsync(
                $"Поздравляем {winner.Name} с победой в игре!",
                logger
            );
        }

        roulette.IsGameRunning = false;
    }

    private async ValueTask<bool> TryToSavePlayer(RouletePlayer shotPlayer)
    {
        if (_noWaifuHelpUsers.Contains(shotPlayer.TwitchId))
        {
            return false;
        }
        await using AppDbContext dbcontext = await factory.CreateDbContextAsync(token);
        Host? host = await dbcontext.Hosts.FindAsync(shotPlayer.TwitchId);

        if (host?.IsPrivated == false)
        {
            return false;
        }

        var chance = Random.Shared.Next(0, 101);
        if (chance < ChanceToBeSaved)
        {
            _noWaifuHelpUsers.Add(shotPlayer.TwitchId);
            return true;
        }

        return false;
    }

    private async Task AloneRoulette(string username, CancellationToken token)
    {
        await client.SendMessageToMainTwitchAsync($"@{username}, я взвожу курок...", logger);
        await Task.Delay(3000, token);
        await client.SendMessageToMainTwitchAsync($"@{username}, 3", logger);
        await Task.Delay(1000, token);
        await client.SendMessageToMainTwitchAsync($"@{username}, 2", logger);
        await Task.Delay(1000, token);
        await client.SendMessageToMainTwitchAsync($"@{username}, 1", logger);
        await Task.Delay(1000, token);

        var rnd = new Random();
        var randomShoot = rnd.Next(1, 7);
        switch (randomShoot)
        {
            case 1:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, сегодня твой день.",
                    logger
                );
                break;
            case 6:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, осечка, но я не думаю, что в следующий раз тебе так повезет.",
                    logger
                );
                break;
            case 3:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, я медленно подвожу ствол к твоему виску. Ничего не происходит. Повезло. Или это просто осечка?",
                    logger
                );
                break;
            case 4:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, повезло. Не уверен, что ты рискнешь еще раз со мной сыграть в эту игру.",
                    logger
                );
                break;
            case 5:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, живой или мертвый ты пойдешь со мной. Но видимо не сегодня.",
                    logger
                );
                break;
            case 2:
                await client.SendMessageToMainTwitchAsync(
                    $"@{username}, BANG! BANG! BANG!",
                    logger
                );
                client.TimeoutUser(
                    TwitchExstension.Channel,
                    username,
                    TimeSpan.FromMinutes(10),
                    "Проиграл(а) в русскую рулетку!"
                );
                break;
        }
    }
}
