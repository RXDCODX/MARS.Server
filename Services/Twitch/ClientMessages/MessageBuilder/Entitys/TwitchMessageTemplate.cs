using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.Entitys;

/// <summary>
/// Шаблон сообщения для Twitch с поддержкой переменных
/// </summary>
public class TwitchMessageTemplate
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Название шаблона
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// Шаблон сообщения с поддержкой переменных
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string MessageTemplate { get; set; }

    /// <summary>
    /// Описание шаблона
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Триггер-слово для активации шаблона
    /// </summary>
    [Required]
    [MaxLength(50)]
    public required string TriggerWord { get; set; }

    /// <summary>
    /// Цвет никнейма автора (в формате hex, например #FF0000)
    /// </summary>
    [MaxLength(7)]
    public string? AuthorColor { get; set; }

    /// <summary>
    /// Имя автора сообщения (если не указано, используется никнейм пользователя)
    /// </summary>
    [MaxLength(50)]
    public string? AuthorName { get; set; }

    /// <summary>
    /// Активен ли шаблон
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Приоритет шаблона (чем выше, тем приоритетнее)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Количество использований
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Коэффициент случайности (0-100, где 100 - всегда срабатывает)
    /// </summary>
    [Range(0, 100)]
    public int RandomChance { get; set; } = 100;

    /// <summary>
    /// Минимальный интервал между срабатываниями в секундах
    /// </summary>
    public int CooldownSeconds { get; set; } = 0;

    /// <summary>
    /// Время последнего срабатывания
    /// </summary>
    public DateTime? LastTriggeredAt { get; set; }
}
