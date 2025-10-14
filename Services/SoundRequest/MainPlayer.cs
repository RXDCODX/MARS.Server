namespace MARS.Server.Services.SoundRequest;

public class MainPlayer
{
    #region Events

    /// <summary>
    /// Событие запуска воспроизведения
    /// </summary>
    public event EventHandler? OnPlay;

    /// <summary>
    /// Событие паузы воспроизведения
    /// </summary>
    public event EventHandler? OnPause;

    /// <summary>
    /// Событие остановки воспроизведения
    /// </summary>
    public event EventHandler? OnStop;

    /// <summary>
    /// Событие возобновления воспроизведения
    /// </summary>
    public event EventHandler? OnResume;

    /// <summary>
    /// Событие переключения на следующий трек
    /// </summary>
    public event EventHandler? OnNext;

    /// <summary>
    /// Событие переключения на предыдущий трек
    /// </summary>
    public event EventHandler? OnPrevious;

    /// <summary>
    /// Событие изменения громкости
    /// </summary>
    public event EventHandler<float>? OnVolumeChanged;

    /// <summary>
    /// Событие включения беззвучного режима
    /// </summary>
    public event EventHandler? OnMuted;

    /// <summary>
    /// Событие выключения беззвучного режима
    /// </summary>
    public event EventHandler? OnUnmuted;

    /// <summary>
    /// Событие окончания трека
    /// </summary>
    public event EventHandler? OnTrackEnded;

    /// <summary>
    /// Событие перемотки
    /// </summary>
    public event EventHandler<TimeSpan>? OnSeek;

    /// <summary>
    /// Событие изменения состояния воспроизведения
    /// </summary>
    public event EventHandler<PlaybackState>? OnPlaybackStateChanged;

    #endregion

    #region Methods

    /// <summary>
    /// Запуск воспроизведения
    /// </summary>
    public void Play() { }

    /// <summary>
    /// Приостановка воспроизведения
    /// </summary>
    public void Pause() { }

    /// <summary>
    /// Остановка воспроизведения
    /// </summary>
    public void Stop() { }

    /// <summary>
    /// Возобновление воспроизведения после паузы
    /// </summary>
    public void Resume() { }

    /// <summary>
    /// Переключение на следующий трек
    /// </summary>
    public void Next() { }

    /// <summary>
    /// Переключение на предыдущий трек
    /// </summary>
    public void Previous() { }

    /// <summary>
    /// Перемотка на указанную позицию
    /// </summary>
    /// <param name="position">Позиция для перемотки</param>
    public void Seek(TimeSpan position) { }

    /// <summary>
    /// Установка громкости
    /// </summary>
    /// <param name="volume">Уровень громкости (0.0 - 1.0)</param>
    public void SetVolume(float volume) { }

    /// <summary>
    /// Включение беззвучного режима
    /// </summary>
    public void Mute() { }

    /// <summary>
    /// Выключение беззвучного режима
    /// </summary>
    public void Unmute() { }

    /// <summary>
    /// Загрузка трека
    /// </summary>
    /// <param name="trackPath">Путь к треку</param>
    public void LoadTrack(string trackPath) { }

    /// <summary>
    /// Получение текущего трека
    /// </summary>
    /// <returns>Путь к текущему треку</returns>
    public string? GetCurrentTrack()
    {
        return null;
    }

    /// <summary>
    /// Получение текущего состояния воспроизведения
    /// </summary>
    /// <returns>Состояние воспроизведения</returns>
    public PlaybackState GetPlaybackState()
    {
        return PlaybackState.Stopped;
    }

    /// <summary>
    /// Получение текущей позиции воспроизведения
    /// </summary>
    /// <returns>Текущая позиция</returns>
    public TimeSpan GetCurrentPosition()
    {
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Получение длительности текущего трека
    /// </summary>
    /// <returns>Длительность трека</returns>
    public TimeSpan GetDuration()
    {
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Получение текущей громкости
    /// </summary>
    /// <returns>Уровень громкости (0.0 - 1.0)</returns>
    public float GetVolume()
    {
        return 1.0f;
    }

    /// <summary>
    /// Проверка беззвучного режима
    /// </summary>
    /// <returns>True если включен беззвучный режим</returns>
    public bool IsMuted()
    {
        return false;
    }

    #endregion
}

/// <summary>
/// Состояние воспроизведения
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// Остановлено
    /// </summary>
    Stopped,

    /// <summary>
    /// Воспроизводится
    /// </summary>
    Playing,

    /// <summary>
    /// На паузе
    /// </summary>
    Paused,

    /// <summary>
    /// Загружается
    /// </summary>
    Loading,
}
