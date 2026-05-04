namespace MARS.Server.Services.Twitch.Entitys;

/// <summary>
/// Запись об активации награды Miku Monday
/// </summary>
public class MikuMondayActivation
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Twitch ID пользователя
    /// </summary>
    public required string TwitchUserId { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// ID трека который выпал
    /// </summary>
    public int MikuMondayTrackId { get; set; }

    /// <summary>
    /// Навигационное свойство к треку
    /// </summary>
    public MikuMondayTrack? MikuMondayTrack { get; set; }

    /// <summary>
    /// Дата активации
    /// </summary>
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Номер недели года (для группировки по понедельникам)
    /// </summary>
    public int WeekOfYear { get; set; }

    /// <summary>
    /// Год
    /// </summary>
    public int Year { get; set; }
}
