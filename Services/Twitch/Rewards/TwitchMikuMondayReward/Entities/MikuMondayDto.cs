using System;
using MARS.Server.Services.Twitch.Entitys;

namespace MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward.Entities;

/// <summary>
/// DTO для передачи данных Miku Monday на фронтенд
/// </summary>
public class MikuMondayDto
{
    /// <summary>
    /// Уникальный идентификатор награды
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public required TwitchUser TwitchUser { get; set; }

    /// <summary>
    /// Трек, который выпал пользователю
    /// </summary>
    public required MikuTrackDto SelectedTrack { get; set; }

    /// <summary>
    /// Список оставшихся свободных треков
    /// </summary>
    public required List<MikuTrackDto> AvailableTracks { get; set; }

    /// <summary>
    /// Флаг, указывающий, что трек не должен списываться из очереди
    /// </summary>
    public bool SkipAvailableTracksUpdate { get; set; }
}

/// <summary>
/// DTO для трека Miku
/// </summary>
public class MikuTrackDto
{
    public required Guid Id { get; set; }
    public int Number { get; set; }
    public required string Artist { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public string? ThumbnailUrl { get; set; }
}
