using MARS.Server.Services.Framedata.Entitys;

namespace MARS.Server.Services.Framedata;

public partial class Tekken8FrameData
{
    private static Task<bool> IsDateInCurrentWeek(DateTimeOffset date)
    {
        DateTimeOffset currentDate = DateTimeOffset.Now;

        // Определяем первый день текущей недели (предполагается, что неделя начинается с понедельника)
        var daysToSubtract = (int)currentDate.DayOfWeek - (int)DayOfWeek.Monday;
        if (daysToSubtract < 0)
        {
            daysToSubtract += 7; // Если сегодня воскресенье (DayOfWeek.Sunday = 0)
        }

        DateTime startOfWeek = currentDate.AddDays(-daysToSubtract).Date;

        // Определяем последний день текущей недели
        DateTime endOfWeek = startOfWeek.AddDays(7);

        // Сравниваем дату с началом и концом недели
        return Task.FromResult(date >= startOfWeek && date <= endOfWeek);
    }

    private static string ReplaceCommandCharacters(string command)
    {
        return string.IsNullOrEmpty(command)
            ? string.Empty
            : Aliases.MoveInputReplacer.Aggregate(
                command,
                (current, r) => current.Replace(r.Key, r.Value)
            );
    }

    public ValueTask<string> GetMoveTags(Move move)
    {
        var tags = new List<string>();

        if (move.Tornado)
        {
            tags.Add("Tornado");
        }

        if (move.HeatEngage)
        {
            tags.Add("Heat Engage");
        }

        if (move.HeatSmash)
        {
            tags.Add("Heat Smash");
        }

        if (move.PowerCrush)
        {
            tags.Add("Power crush");
        }

        if (move.Homing)
        {
            tags.Add("Homing");
        }

        if (move.RequiresHeat)
        {
            tags.Add("Heat");
        }

        if (move.Throw)
        {
            tags.Add("Throw");
        }

        if (!string.IsNullOrWhiteSpace(move.StanceCode))
        {
            if (move.StanceName != null)
            {
                tags.Add(move.StanceName);
            }
        }

        return ValueTask.FromResult(string.Join(",", tags));
    }
}
