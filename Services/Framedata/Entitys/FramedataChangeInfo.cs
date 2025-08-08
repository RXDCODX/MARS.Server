using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.Framedata.Entitys;

/// <summary>
/// Представляет информацию об изменении в фреймдате
/// </summary>
public class FramedataChangeInfo
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Ссылка на изменение
    /// </summary>
    public int FramedataChangeId { get; set; }
    public FramedataChange? FramedataChange { get; set; }
    
    /// <summary>
    /// Ссылка на изменение для CurrentInfo (nullable)
    /// </summary>
    public int? CurrentInfoId { get; set; }
    
    /// <summary>
    /// Тип информации
    /// </summary>
    public FramedataInfoType InfoType { get; set; }
    
    /// <summary>
    /// JSON данные (сериализованная информация)
    /// </summary>
    [Required]
    public required string JsonData { get; set; }
    
    /// <summary>
    /// URL источника данных
    /// </summary>
    [MaxLength(500)]
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Время получения данных
    /// </summary>
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.Now;
    
    /// <summary>
    /// Хеш данных для сравнения
    /// </summary>
    [MaxLength(64)]
    public string? DataHash { get; set; }
}

/// <summary>
/// Тип информации
/// </summary>
public enum FramedataInfoType
{
    /// <summary>
    /// Информация о персонаже
    /// </summary>
    Character,
    
    /// <summary>
    /// Информация о ходе
    /// </summary>
    Move,
    
    /// <summary>
    /// Список ходов персонажа
    /// </summary>
    Movelist
}
