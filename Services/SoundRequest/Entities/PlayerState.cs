using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.SoundRequest.Entities;

public class PlayerState : ICloneable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public BaseTrackInfo? CurrentTrack { get; set; }

    public BaseTrackInfo? NextTrack { get; set; }

    public TimeSpan? CurrentTrackDuration { get; set; }

    public bool IsPaused { get; set; }

    public bool IsMuted { get; set; }

    public bool IsStoped { get; set; }

    public int Volume { get; set; } = 100;

    public object Clone()
    {
        return MemberwiseClone();
    }

    public event EntityChanged? EntityChanged;

    public void ChangeEntity()
    {
        EntityChanged?.Invoke(this, EventArgs.Empty);
    }
}

public delegate Task EntityChanged(object? sender, EventArgs eventargs);

public static class PlayerStateUpdater
{
    public static void UpdatePlayerState(this PlayerState sourceState, Action<PlayerState> upAction)
    {
        var newState = sourceState.Clone();

        if (newState is PlayerState state)
        {
            upAction(state);
            sourceState = state;
            sourceState.ChangeEntity();
            return;
        }

        throw new InvalidCastException();
    }
}


