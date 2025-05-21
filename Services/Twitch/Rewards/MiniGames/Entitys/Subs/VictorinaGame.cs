using System.Text;

namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;

public class VictorinaGame(ILogger<TwitchTrivia> logger, ITwitchClient client, TwitchTrivia trivia)
{
    private readonly List<VictorinaLetter> _listLetters = [];
    public string Answer = "";

    public bool Active { get; set; } = true;
    public bool AllLettersShowed { get; set; }
    public bool SkipQuestion { get; set; }

    private async Task StartQuestion()
    {
        var numberQuestion = Random.Shared.Next(0, trivia.CountQuestions);
        var strQuestion = await GetQuestion(numberQuestion);
        var arrQuestion = strQuestion.Split('|');
        Answer = arrQuestion[1];

        foreach (var t in Answer)
        {
            _listLetters.Add(new VictorinaLetter { Letter = t, Showed = false });
        }

        await client.SendMessageToMainTwitchAsync(
            $"({Answer.Length} букв): {arrQuestion[0]}",
            logger
        );
    }

    private async Task<string> GetQuestion(int numberQuestion)
    {
        var lines = await File.ReadAllLinesAsync(trivia.FilenameTrivia);

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
                await Task.Delay(trivia.TimeoutBetweenHints * 1000, trivia.TokenSource!.Token);

                if (!Active)
                {
                    return;
                }

                await trivia.SemaphoreSlim.WaitAsync(trivia.TokenSource.Token);
                if (SkipQuestion)
                {
                    SkipQuestion = false;
                    break;
                }

                trivia.SemaphoreSlim.Release();

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

                await trivia.SemaphoreSlim.WaitAsync(trivia.TokenSource.Token);
                if (SkipQuestion)
                {
                    SkipQuestion = false;
                    break;
                }

                trivia.SemaphoreSlim.Release();

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
                await trivia.SemaphoreSlim.WaitAsync(trivia.TokenSource.Token);

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
                    await client.SendMessageToMainTwitchAsync(
                        $"Никто не отгадал! Ответ: {strHint}",
                        logger
                    );
                    trivia.NoWaifuHelpUsers.Clear();
                }
                else
                {
                    await client.SendMessageToMainTwitchAsync($"Подсказка: {strHint}", logger);
                }

                trivia.SemaphoreSlim.Release();
            }

            Active = false;
            SkipQuestion = false;
        }
        catch (Exception ex)
        {
            logger.LogException(ex);
        }
        finally
        {
            Active = false;
            trivia.IsGameRunning = false;
        }
    }
}
