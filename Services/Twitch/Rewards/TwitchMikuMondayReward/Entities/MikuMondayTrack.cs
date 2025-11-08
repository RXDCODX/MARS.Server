using System.ComponentModel.DataAnnotations;
using MARS.Server.Services.SoundRequest.Entities;

namespace MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward.Entities;

/// <summary>
/// Связующая таблица между наградой Miku Monday и BaseTrackInfo
/// Хранит только номер трека в плейлисте Miku
/// </summary>
public class MikuMondayTrack
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Номер трека в списке Miku (1-27)
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// ID трека в BaseTrackInfo
    /// </summary>
    public Guid BaseTrackInfoId { get; set; }

    /// <summary>
    /// Навигационное свойство к базовой информации о треке
    /// </summary>
    public BaseTrackInfo? BaseTrackInfo { get; set; }

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}












