namespace MARS.Server.Services.SoundRequest.Entities;

/// <summary>
/// Состояния воспроизведения плеера
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// Плеер остановлен, нет активного трека
    /// </summary>
    Stopped = 0,

    /// <summary>
    /// Трек воспроизводится
    /// </summary>
    Playing = 1,

    /// <summary>
    /// Воспроизведение приостановлено
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Происходит переключение между треками
    /// </summary>
    SwitchingTrack = 3,

    /// <summary>
    /// Ожидание добавления трека в очередь
    /// </summary>
    WaitingForTrack = 4
}

