using System.IO;
using MARS.Server.Services.DatabaseBackup.Models;

namespace MARS.Server.Services.DatabaseBackup;

/// <summary>
/// Сервис для создания резервных копий базы данных
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Создает резервную копию базы данных
    /// </summary>
    /// <param name="databaseName">Имя базы данных для резервного копирования</param>
    /// <returns>URL для скачивания резервной копии</returns>
    Task<string> CreateBackupAsync(string databaseName);
    
    /// <summary>
    /// Получает файл резервной копии по имени файла
    /// </summary>
    /// <param name="fileName">Имя файла резервной копии</param>
    /// <returns>Поток с данными резервной копии</returns>
    Task<Stream> GetBackupFileAsync(string fileName);
    
    /// <summary>
    /// Получает список доступных резервных копий
    /// </summary>
    /// <returns>Список имен файлов резервных копий</returns>
    Task<IEnumerable<string>> GetAvailableBackupsAsync();
    
    /// <summary>
    /// Удаляет старые резервные копии, оставляя только последние N
    /// </summary>
    /// <param name="keepCount">Количество копий для сохранения</param>
    /// <returns>Количество удаленных файлов</returns>
    Task<int> CleanupOldBackupsAsync(int keepCount = 10);
    
    /// <summary>
    /// Получает информацию о файле резервной копии
    /// </summary>
    /// <param name="fileName">Имя файла</param>
    /// <returns>Информация о файле или null если файл не найден</returns>
    Task<BackupFileInfo?> GetBackupFileInfoAsync(string fileName);
}
