using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.Framedata.Entitys;

/// <summary>
/// Представляет изменение в фреймдате персонажа
/// </summary>
public class FramedataChange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Имя персонажа
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string CharacterName { get; set; }

    /// <summary>
    /// Тип изменения
    /// </summary>
    public FramedataChangeType ChangeType { get; set; }

    /// <summary>
    /// Время обнаружения изменения
    /// </summary>
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Время применения изменения (null если не применено)
    /// </summary>
    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>
    /// Статус изменения
    /// </summary>
    public FramedataChangeStatus Status { get; set; } = FramedataChangeStatus.Pending;

    /// <summary>
    /// Описание изменения
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Ссылка на новую информацию
    /// </summary>
    public FramedataChangeInfo? ChangeInfo { get; set; }

    /// <summary>
    /// Ссылка на актуальную информацию
    /// </summary>
    public FramedataChangeInfo? CurrentInfo { get; set; }
}

/// <summary>
/// Тип изменения в фреймдате
/// </summary>
public enum FramedataChangeType
{
    /// <summary>
    /// Новый персонаж
    /// </summary>
    NewCharacter,

    /// <summary>
    /// Новый ход
    /// </summary>
    NewMove,

    /// <summary>
    /// Изменение существующего хода
    /// </summary>
    MoveUpdate,

    /// <summary>
    /// Удаление хода
    /// </summary>
    MoveRemoval,

    /// <summary>
    /// Обновление информации о персонаже
    /// </summary>
    CharacterUpdate,
}

/// <summary>
/// Статус изменения
/// </summary>
public enum FramedataChangeStatus
{
    /// <summary>
    /// Ожидает применения
    /// </summary>
    Pending,

    /// <summary>
    /// Применено
    /// </summary>
    Applied,

    /// <summary>
    /// Отклонено
    /// </summary>
    Rejected,

    /// <summary>
    /// Устарело
    /// </summary>
    Obsolete,
}
