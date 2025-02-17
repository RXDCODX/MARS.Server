using System.Text;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;

public class VictorinaGame
{
    private readonly ITwitchClient _client;
    private readonly List<VictorinaLetter> _listLetters = new();
    private readonly ILogger<TwitchTrivia> _logger;
    private readonly TwitchTrivia _trivia;
    public string Answer = "";

    public VictorinaGame(ILogger<TwitchTrivia> logger, ITwitchClient client, TwitchTrivia trivia)
    {
        _logger = logger;
        _client = client;
        _trivia = trivia;
    }

    public bool Active { get; set; } = true;
    public bool AllLettersShowed { get; set; }
    public bool SkipQuestion { get; set; }

    private async Task StartQuestion()
    {
        var numberQuestion = Random.Shared.Next(0, _trivia.CountQuestions);
        var strQuestion = await GetQuestion(numberQuestion);
        var arrQuestion = strQuestion.Split('|');
        Answer = arrQuestion[1];

        foreach (var t in Answer)
        {
            _listLetters.Add(new VictorinaLetter { Letter = t, Showed = false });
        }

        await _client.SendMessageToPyrokxnezxzAsync(
            $"({Answer.Length} букв): {arrQuestion[0]}",
            _logger
        );
    }

    private async Task<string> GetQuestion(int numberQuestion)
    {
        var lines = await File.ReadAllLinesAsync(_trivia.FilenameTrivia);

        do
        {
            if (numberQuestion > lines.Length)
            {
                numberQuestion = Random.Shared.Next(1, lines.Length - 1);
            }
        } while (lines.Length < numberQuestion);

        return lines[numberQuestion];
    }

    public async void MainThread()
    {
        try
        {
            AllLettersShowed = false;
            SkipQuestion = false;
            await StartQuestion();

            while (!AllLettersShowed && !SkipQuestion)
            {
                await Task.Delay(_trivia.TimeoutBetweenHints * 1000, _trivia.TokenSource!.Token);

                if (!Active)
                {
                    return;
                }

                await _trivia.SemaphoreSlim.WaitAsync(_trivia.TokenSource.Token);
                if (SkipQuestion)
                {
                    SkipQuestion = false;
                    break;
                }

                _trivia.SemaphoreSlim.Release();

                var founded = false;
                while (!founded)
                {
                    var indLetter = Random.Shared.Next(0, Answer.Length);
                    if (!_listLetters[indLetter].Showed)
                    {
                        _listLetters[indLetter].Showed = true;
                        founded = true;
                    }
                }

                await _trivia.SemaphoreSlim.WaitAsync(_trivia.TokenSource.Token);
                if (SkipQuestion)
                {
                    SkipQuestion = false;
                    break;
                }

                _trivia.SemaphoreSlim.Release();

                //вывод подсказки
                var strHint = new StringBuilder("");
                foreach (VictorinaLetter itemLetter in _listLetters)
                {
                    if (itemLetter.Showed)
                    {
                        strHint.Append(itemLetter.Letter);
                    }
                    else
                    {
                        strHint.Append("_");
                    }

                    strHint.Append(" ");
                }

                //если отгадали otgadali = true; break
                await _trivia.SemaphoreSlim.WaitAsync(_trivia.TokenSource.Token);

                if (SkipQuestion)
                {
                    SkipQuestion = false;
                    break;
                }

                if (AllLettersShowed)
                {
                    break;
                }

                //Не показывать полностью подсказку а объявить об окончании вопроса
                var countLetters = _listLetters.Count(e => e.Showed);
                var allLettersAreValid = countLetters == _listLetters.Count;

                if (allLettersAreValid)
                {
                    AllLettersShowed = true;
                    await _client.SendMessageToPyrokxnezxzAsync(
                        $"Никто не отгадал! Ответ: {strHint}",
                        _logger
                    );
                }
                else
                {
                    await _client.SendMessageToPyrokxnezxzAsync($"Подсказка: {strHint}", _logger);
                }

                _trivia.SemaphoreSlim.Release();
            }

            Active = false;
            SkipQuestion = false;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
        finally
        {
            Active = false;
            _trivia.IsGameActive = false;
        }
    }
}
