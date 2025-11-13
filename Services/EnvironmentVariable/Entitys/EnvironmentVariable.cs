namespace MARS.Server.Services.EnvironmentVariable.Entitys;

/// <summary>
/// Переменная окружения, хранимая в базе данных
/// </summary>
public class EnvironmentVariable
{
    /// <summary>
    /// Ключ переменной окружения
    /// </summary>
    [Key]
    public required string Key { get; set; }

    /// <summary>
    /// Значение переменной окружения
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Описание переменной
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Время создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
