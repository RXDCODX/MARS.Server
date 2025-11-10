namespace MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward.Entities;

/// <summary>
/// Результат получения трека Miku Monday
/// </summary>
public class MikuMondayResult
{
    /// <summary>
    /// Выбранный трек (null если ошибка)
    /// </summary>
    public MikuMondayTrack? Track { get; set; }

    /// <summary>
    /// Список оставшихся доступных треков
    /// </summary>
    public List<MikuMondayTrack> AvailableTracks { get; set; } = [];

    /// <summary>
    /// Сообщение об ошибке (null если успех)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Успешно ли получен трек
    /// </summary>
    public bool IsSuccess => Track != null && string.IsNullOrWhiteSpace(Error);
}
