namespace MARS.Server.Services.DatabaseBackup.Entitys;

/// <summary>
/// Модель информации о файле резервной копии
/// </summary>
public class BackupFileInfo
{
    /// <summary>
    /// Имя файла
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Имя базы данных
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Размер файла в МБ
    /// </summary>
    public double SizeMB => Math.Round(Size / (1024.0 * 1024.0), 2);

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// MIME-тип файла
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// URL для скачивания
    /// </summary>
    public string DownloadUrl => $"/memory/{FileName}";
}
