namespace MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.DTOs;

/// <summary>
/// DTO для создания шаблона сообщения
/// </summary>
public class CreateTwitchMessageTemplateDto
{
    /// <summary>
    /// Название шаблона
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Шаблон сообщения с поддержкой переменных
    /// </summary>
    public required string MessageTemplate { get; set; }

    /// <summary>
    /// Описание шаблона
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Триггер-слово для активации шаблона
    /// </summary>
    public required string TriggerWord { get; set; }

    /// <summary>
    /// Цвет никнейма автора (в формате hex, например #FF0000)
    /// </summary>
    public string? AuthorColor { get; set; }

    /// <summary>
    /// Имя автора сообщения (если не указано, используется никнейм пользователя)
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Приоритет шаблона (чем выше, тем приоритетнее)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Коэффициент случайности (0-100, где 100 - всегда срабатывает)
    /// </summary>
    public int RandomChance { get; set; } = 100;

    /// <summary>
    /// Минимальный интервал между срабатываниями в секундах
    /// </summary>
    public int CooldownSeconds { get; set; } = 0;
}

/// <summary>
/// DTO для обновления шаблона сообщения
/// </summary>
public class UpdateTwitchMessageTemplateDto
{
    /// <summary>
    /// Название шаблона
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Шаблон сообщения с поддержкой переменных
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    /// Описание шаблона
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Триггер-слово для активации шаблона
    /// </summary>
    public string? TriggerWord { get; set; }

    /// <summary>
    /// Цвет никнейма автора (в формате hex, например #FF0000)
    /// </summary>
    public string? AuthorColor { get; set; }

    /// <summary>
    /// Имя автора сообщения (если не указано, используется никнейм пользователя)
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Активен ли шаблон
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Приоритет шаблона (чем выше, тем приоритетнее)
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Коэффициент случайности (0-100, где 100 - всегда срабатывает)
    /// </summary>
    public int? RandomChance { get; set; }

    /// <summary>
    /// Минимальный интервал между срабатываниями в секундах
    /// </summary>
    public int? CooldownSeconds { get; set; }
}

/// <summary>
/// DTO для отображения шаблона сообщения
/// </summary>
public class TwitchMessageTemplateResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerWord { get; set; } = string.Empty;
    public string? AuthorColor { get; set; }
    public string? AuthorName { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public int RandomChance { get; set; }
    public int CooldownSeconds { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
}
