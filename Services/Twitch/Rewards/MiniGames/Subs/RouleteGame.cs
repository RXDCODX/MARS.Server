using TwitchLib.Client.Extensions;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;

public class RouleteGame
{
    private const int ChanceToBeSaved = 40;
    private readonly ITwitchClient _client;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<TwitchRussianRoulete> _logger;

    private readonly CancellationToken _token;

    public RouleteGame(
        List<RouletePlayer> players,
        GameType type,
        CancellationToken token,
        ITwitchClient client,
        ILogger<TwitchRussianRoulete> logger,
        IDbContextFactory<AppDbContext> factory
    )
    {
        Players = players.ToList();
        Type = type;
        _token = token;
        _client = client;
        _logger = logger;
        _factory = factory;
    }

    private List<RouletePlayer> Players { get; }
    private GameType Type { get; }

    public async Task RussianRoulette()
    {
        var numPlayers = Players.Count;
        var roundNum = 1;
        var random = new Random();

        if (numPlayers == 1)
        {
            await AloneRoulette(Players[0].Name, _token);
            return;
        }

        if (numPlayers == 2)
        {
            var namesForMinigame = string.Join(", ", Players.Select(player => player.Name));
            await _client.SendMessageToPyrokxnezxzAsync(
                $"Играется рулетка на двоих! Играют: {namesForMinigame}",
                _logger
            );
        }

        while (Players.Count(e => e.IsAlive) > 1 && !_token.IsCancellationRequested)
        {
            var alivePlayers = Players.Where(player => player.IsAlive).ToList();
            if (Type != GameType.MiniGame)
            {
                var alivePlayersNames = string.Join(
                    ", ",
                    alivePlayers.Select(player => player.Name)
                );
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"Русская рулетка - раунд {roundNum}! Играют: {alivePlayersNames}",
                    _logger
                );
            }

            var index = random.Next(alivePlayers.Count(e => e.IsAlive));
            await Task.Delay(1000, _token);
            RouletePlayer shotPlayer = alivePlayers[index];

            var isSaved = await TryToSavePlayer(shotPlayer);

            if (isSaved)
            {
                await using AppDbContext context =
                    await _factory.CreateDbContextAsync(_token) ?? throw new Exception("еблан?");
                Host host =
                    context.Hosts.Find(shotPlayer.TwitchId)
                    ?? throw new NullReferenceException(
                        "Обращение к спассеному host'у который был спасен"
                    );
                Waifu waifu =
                    await context.Waifus.FirstOrDefaultAsync(
                        e => e.ShikiId == host.WaifuBrideId,
                        _token
                    ) ?? throw new NullReferenceException("не найдена зарегестрированная жена");
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{shotPlayer.Name}, твой супруг - {waifu.Name} спас тебя от неминуемой гибели!",
                    _logger
                );
            }
            else
            {
                await _client.SendMessageToPyrokxnezxzAsync(
                    StaticContent.PlayerEliminated(shotPlayer.Name),
                    _logger
                );
                shotPlayer.IsAlive = false;
                //_client.TimeoutUser(TwitchExstension.Channel, shotPlayer.Name, TimeSpan.FromMinutes(10), "Ты вмер!");
            }

            await Task.Delay(2000, _token);

            roundNum++;
        }

        RouletePlayer winner = Players.First(e => e.IsAlive);
        if (Type == GameType.MiniGame)
        {
            await _client.SendMessageToPyrokxnezxzAsync(
                $"Победитель: {winner.Name}. {StaticContent.GetMiniHistory(winner.Name)}",
                _logger
            );
        }
        else
        {
            await _client.SendMessageToPyrokxnezxzAsync(
                $"Поздравляем {winner.Name} с победой в игре!",
                _logger
            );
        }
    }

    private async ValueTask<bool> TryToSavePlayer(RouletePlayer shotPlayer)
    {
        await using AppDbContext dbcontext = await _factory.CreateDbContextAsync(_token);
        Host? host = await dbcontext.Hosts.FindAsync(shotPlayer.TwitchId);

        if (host?.IsPrivated == false)
        {
            return false;
        }

        var chance = Random.Shared.Next(0, 101);
        if (chance < ChanceToBeSaved)
        {
            return true;
        }

        return false;
    }

    private async Task AloneRoulette(string username, CancellationToken token)
    {
        await _client.SendMessageToPyrokxnezxzAsync($"@{username}, я взвожу курок...", _logger);
        await Task.Delay(3000, token);
        await _client.SendMessageToPyrokxnezxzAsync($"@{username}, 3", _logger);
        await Task.Delay(1000, token);
        await _client.SendMessageToPyrokxnezxzAsync($"@{username}, 2", _logger);
        await Task.Delay(1000, token);
        await _client.SendMessageToPyrokxnezxzAsync($"@{username}, 1", _logger);
        await Task.Delay(1000, token);

        var rnd = new Random();
        var randomShoot = rnd.Next(1, 7);
        switch (randomShoot)
        {
            case 1:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, сегодня твой день.",
                    _logger
                );
                break;
            case 6:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, осечка, но я не думаю, что в следующий раз тебе так повезет.",
                    _logger
                );
                break;
            case 3:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, я медленно подвожу ствол к твоему виску. Ничего не происходит. Повезло. Или это просто осечка?",
                    _logger
                );
                break;
            case 4:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, повезло. Не уверен, что ты рискнешь еще раз со мной сыграть в эту игру.",
                    _logger
                );
                break;
            case 5:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, живой или мертвый ты пойдешь со мной. Но видимо не сегодня.",
                    _logger
                );
                break;
            case 2:
                await _client.SendMessageToPyrokxnezxzAsync(
                    $"@{username}, BANG! BANG! BANG!",
                    _logger
                );
                _client.TimeoutUser(
                    TwitchExstension.Channel,
                    username,
                    TimeSpan.FromMinutes(10),
                    "Проиграл(а) в русскую рулетку!"
                );
                break;
        }
    }
}
