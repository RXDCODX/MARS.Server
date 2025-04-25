using MARS.Server.Services.Framedata;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Twitch.Management;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Interfaces;
using MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Client.Events;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames;

public class TekkenVictorina(
    ITwitchClient client,
    ITwitchAPI api,
    TokenService tokenService,
    Tekken8FrameData frameData,
    IHostApplicationLifetime lifetime,
    ILogger<TekkenVictorina> logger
) : BackgroundService, ITwitchMiniGame
{
    private CancellationToken? _cancellationToken = lifetime.ApplicationStopping;
    public bool IsGameRunning { get; set; } = false;
    private const string? CommandForStop = "!стопвикторина";
    private TekkenVictorinaGame? _currentGame;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            client.OnMessageReceived += TwitchClientOnOnMessageReceived;
        });
        return Task.CompletedTask;
    }

    private async void TwitchClientOnOnMessageReceived(
        object? sender,
        OnMessageReceivedArgs onMessageReceivedArgs
    )
    {
        await Task.Factory.StartNew(async () =>
        {
            var name = onMessageReceivedArgs.ChatMessage.Username;
            var message = onMessageReceivedArgs.ChatMessage.Message;
            var id = onMessageReceivedArgs.ChatMessage.UserId;

            if (name == TwitchExstension.BotName)
            {
                return;
            }

            //Стоп - слово
            if (
                message.Equals(CommandForStop, StringComparison.OrdinalIgnoreCase)
                && name.Equals(TwitchExstension.Channel, StringComparison.OrdinalIgnoreCase)
            )
            {
                if (_currentGame is { Active: true })
                {
                    _currentGame.Active = false;
                    _currentGame = null;
                    await client.SendMessageToMainTwitchAsync("Теккен викторина была остановлена!");
                }
                else
                {
                    await client.SendMessageToMainTwitchAsync("Теккен викторина не была запущена.");
                }

                return;
            }

            if (_currentGame != null)
            {
                CheckIsAnswer(name, message);
            }
        });
    }

    public int GetGameCost()
    {
        return 8;
    }

    public async Task GameStart(string userName, string userId)
    {
        if (_currentGame is null)
        {
            var randomIndex = Random.Shared.Next(frameData.VictorinaMoves.Count) - 1;
            var randomMove = frameData.VictorinaMoves[randomIndex];
            var awaitTime = TimeSpan.FromSeconds(20);
            var startTime = DateTime.Now;

            var prepare = $"""
                @{userName} начал(а) новую теккен викторину! Нужно назвать фреймдату на блоке для: {string.Concat(
                    randomMove.Character!.Name[0].ToString().ToUpper(),
                    randomMove.Character!.Name.AsSpan(1)
                )} {randomMove.Command} в течении {awaitTime.TotalSeconds} секунд! Принимается ответ в формате: -14 или -14~-11
                """;

            await api.SendAnnouncementToMainTwitch(prepare, tokenService.Token, null, logger);
            var answer = GetAnswer(randomMove);
            _currentGame = new TekkenVictorinaGame(answer);
            var token = _currentGame.CancellationTokenForRightAnswer.Token;

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.Now;
                if (now - startTime >= awaitTime)
                {
                    break;
                }

                await Task.Delay(100, CancellationToken.None);
            }

            if (_currentGame.CancellationTokenForRightAnswer.IsCancellationRequested)
            {
                var rightAnswer = _currentGame.GoodAnswers.First();
                await api.SendAnnouncementToMainTwitch(
                    $"У нас есть победитель в теккен викторине! Поздравляем {rightAnswer.displayName} с ответом {rightAnswer.answer} sigma",
                    tokenService.Token,
                    AnnouncementColors.Primary,
                    logger
                );
                ClearGame();
            }
            else if (_currentGame.GoodAnswers.Count == 0)
            {
                await client.SendMessageToMainTwitchAsync(
                    $"Никто не попытался ответить на теккен викторину Sadge Ответ - {_currentGame.Answer} plinkge"
                );
                ClearGame();
            }
            else
            {
                if (_currentGame.GoodAnswers.Count == 1)
                {
                    var goodAnswer = _currentGame.GoodAnswers.First();
                    await client.SendMessageToMainTwitchAsync(
                        $"Наиболее подходящий ответ на теккен викторине был от {goodAnswer.displayName} с текстом {goodAnswer.answer} Sippin Ответ - {_currentGame.Answer} plinkge"
                    );
                    ClearGame();
                }
                else
                {
                    var answers = string.Join(
                        ',',
                        _currentGame.GoodAnswers.Select(e => $" {e.displayName} с {e.answer}")
                    );
                    await client.SendMessageToMainTwitchAsync(
                        $"Наиболее подходящие ответы на теккен викторину: {answers} DogLookingSussyAndCold Ответ - {_currentGame.Answer}"
                    );
                    ClearGame();
                }
            }
        }
        else
        {
            await client.SendMessageToMainTwitchAsync("Теккен викторина уже используется!");
        }
    }

    private void ClearGame()
    {
        _currentGame = null;
        IsGameRunning = false;
    }

    private IntRange GetAnswer(Move tekkenMove)
    {
        if (int.TryParse(tekkenMove.BlockFrame, out var answer))
        {
            return new IntRange(answer, answer);
        }

        var split = tekkenMove.BlockFrame!.Split('~');
        if (split is { Length: 2 })
        {
            var start = int.Parse(split[0]);
            var end = int.Parse(split[1]);
            return new IntRange(start, end);
        }

        throw new Exception(
            $"Кривой инпут к удара, {tekkenMove.Character?.Name ?? tekkenMove.CharacterName} {tekkenMove.Command}"
        );
    }

    private bool CheckIsAnswer(string displayName, string input)
    {
        if (_currentGame is not { Active: true })
        {
            return false;
        }

        // Парсим ввод пользователя (может быть число или диапазон)
        IntRange? userRange = TryParseInput(input);
        if (!userRange.HasValue)
        {
            return false;
        }

        var answerRange = _currentGame.Answer;

        // Проверяем пересечение диапазонов
        var isIntersect =
            userRange.Value.Start <= answerRange.End && userRange.Value.End >= answerRange.Start;

        // Вычисляем расстояние между диапазонами
        var distance = CalculateDistance(userRange.Value, answerRange);

        if (distance == 0)
        {
            AddOrUpdateGoodAnswer(displayName, userRange.Value);
            _currentGame.CancellationTokenForRightAnswer.Cancel();
            return true;
        }

        // Если диапазоны пересекаются - это точный ответ (distance = 0)
        if (isIntersect)
        {
            AddOrUpdateGoodAnswer(displayName, userRange.Value);
            return true;
        }

        // Получаем текущее минимальное расстояние
        var currentBestDistance = GetCurrentBestDistance();

        // Если список пустой, или новый ответ лучше
        if (_currentGame.GoodAnswers.Count == 0 || distance < currentBestDistance)
        {
            _currentGame.GoodAnswers.Clear();
            AddOrUpdateGoodAnswer(displayName, userRange.Value);
        }
        // Если новый ответ такой же хороший как текущие лучшие
        else if (distance == currentBestDistance)
        {
            AddOrUpdateGoodAnswer(displayName, userRange.Value);
        }

        return isIntersect;

        // Парсит строку в IntRange (число или диапазон)
        IntRange? TryParseInput(string str)
        {
            str = str.Trim();

            // Пробуем распарсить как единичное число
            if (int.TryParse(str, out var singleNumber))
            {
                return new IntRange(singleNumber, singleNumber);
            }

            // Пробуем распарсить как диапазон
            var parts = str.Split('~');
            if (
                parts.Length == 2
                && int.TryParse(parts[0].Trim(), out var start)
                && int.TryParse(parts[1].Trim(), out var end)
            )
            {
                return new IntRange(Math.Min(start, end), Math.Max(start, end));
            }

            return null;
        }
    }

    // Вычисляет минимальное расстояние между диапазонами
    private int CalculateDistance(IntRange a, IntRange b)
    {
        if (a.Start > b.End)
        {
            return a.Start - b.End;
        }

        if (b.Start > a.End)
        {
            return b.Start - a.End;
        }

        return 0; // если есть пересечение
    }

    // Добавляет или обновляет ответ пользователя
    private void AddOrUpdateGoodAnswer(string displayName, IntRange answer)
    {
        if (_currentGame != null)
        {
            var existingIndex = _currentGame.GoodAnswers.FindIndex(x =>
                x.displayName == displayName
            );
            if (existingIndex >= 0)
            {
                _currentGame.GoodAnswers[existingIndex] = (displayName, answer);
            }
            else
            {
                _currentGame.GoodAnswers.Add((displayName, answer));
            }
        }
    }

    // Получает текущее минимальное расстояние среди лучших ответов
    private int GetCurrentBestDistance()
    {
        if (_currentGame is { GoodAnswers.Count: 0 })
        {
            return int.MaxValue;
        }

        return _currentGame!.GoodAnswers.Min(x => CalculateDistance(x.answer, _currentGame.Answer));
    }
}
