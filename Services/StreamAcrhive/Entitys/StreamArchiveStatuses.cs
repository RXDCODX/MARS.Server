namespace MARS.Server.Services.StreamAcrhive.Entitys;

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
    Skipped = 4
}

/// <summary>
/// Статус загрузки части файла
/// </summary>
public enum StreamArchiveChunkStatus
{
    /// <summary>
    /// Часть создана, ожидает загрузки
    /// </summary>
    Created = 0,

    /// <summary>
    /// Часть в процессе загрузки
    /// </summary>
    Uploading = 1,

    /// <summary>
    /// Часть успешно загружена
    /// </summary>
    Uploaded = 2,

    /// <summary>
    /// Ошибка при загрузке части
    /// </summary>
    Failed = 3
}
