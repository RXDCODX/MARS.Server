namespace MARS.Server.Services.SoundRequest.Entities;

/// <summary>
/// Режим отображения видео в плеере
/// </summary>
public enum VideoDisplay
{
    /// <summary>
    /// Воспроизведение с видео (стандартный режим)
    /// </summary>
    Video,

    /// <summary>
    /// Воспроизведение без видео, но с визуализацией
    /// </summary>
    NoVideo,

    /// <summary>
    /// Только аудио без визуального отображения
    /// </summary>
    AudioOnly,
}
