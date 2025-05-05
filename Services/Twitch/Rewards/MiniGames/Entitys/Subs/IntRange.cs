namespace MARS.Server.Services.Twitch.Rewards.MiniGames.Entitys.Subs;

public readonly struct IntRange(int start, int end)
{
    public int Start { get; } = start;
    public int End { get; } = end;

    public bool Contains(int value) => value >= Start && value <= End;

    public int Length => End - Start + 1;

    public override string ToString() => Start == End ? $"[{Start}]" : $"[{Start}~{End}]";
}
