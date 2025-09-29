namespace MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;

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
    Failed = 3,
}
