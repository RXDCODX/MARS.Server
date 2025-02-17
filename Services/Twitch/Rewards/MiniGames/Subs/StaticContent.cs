namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Subs;

public class StaticContent
{
    private static readonly string[] Messages =
    {
        "{0} выбывает из игры.",
        "Конец игры для {0}.",
        "Пока-пока, {0}.",
        "Игрок {0} покидает игру.",
        "Прощай, {0}.",
        "Мы будем скучать по тебе, {0}.",
        "Игрок {0} выбыл из игры.",
        "До свидания, {0}.",
        "Игрок {0} прощается с игрой.",
    };

    private static readonly string[] Historys =
    {
        "Он был на грани жизни и смерти, но ему удалось выжить!",
        "Он смог остаться в живых и победить своего оппонента!",
        "Он был настолько храбр, что смог пройти через смертельную игру!",
        "Его превосходная удача помогла ему победить в смертельной игре!",
        "Ему удалось выжить в этой смертельной игре и одолеть своего оппонента!",
    };

    internal static string GetMiniHistory(string winner)
    {
        var random = Random.Shared;
        var index = random.Next(Historys.Length);
        return winner + " смог победить в игре! " + Historys[index];
    }

    internal static string PlayerEliminated(string playerName)
    {
        var random = Random.Shared;
        var message = Messages[random.Next(Messages.Length)];
        message = string.Format(message, playerName);
        return message;
    }
}
