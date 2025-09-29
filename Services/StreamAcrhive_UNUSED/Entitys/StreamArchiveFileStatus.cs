namespace MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;

/// <summary>
/// Статус обработки файла
/// </summary>
public enum StreamArchiveFileStatus
{
    /// <summary>
    /// Файл обнаружен, ожидает обработки
    /// </summary>
    Discovered = 0,

    /// <summary>
    /// Файл в процессе обработки
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Файл успешно обработан и загружен
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Ошибка при обработке файла
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Файл пропущен (например, уже существует в базе)
    /// </summary>
    Skipped = 4,
}
