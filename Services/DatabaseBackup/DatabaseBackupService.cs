using System.Diagnostics;
using System.Text;
using MARS.Server.Services.DatabaseBackup.Entitys;
using MARS.Server.Services.MemoryStorageService;

namespace MARS.Server.Services.DatabaseBackup;

/// <summary>
/// Сервис для создания резервных копий базы данных PostgreSQL с использованием MemoryStorage
/// </summary>
public class DatabaseBackupService(
    IConfiguration configuration,
    ILogger<DatabaseBackupService> logger,
    IPgDumpSettingsService pgDumpSettingsService
) : IDatabaseBackupService
{
    // Определяем путь к pg_dump

    /// <summary>
    /// Создает резервную копию базы данных
    /// </summary>
    public async Task<string> CreateBackupAsync(string databaseName)
    {
        try
        {
            // Проверяем настройки pg_dump
            var pgDumpSettings = await pgDumpSettingsService.GetActiveSettingsAsync();
            if (pgDumpSettings == null)
            {
                throw new InvalidOperationException(
                    "Настройки pg_dump не найдены. Пожалуйста, настройте путь к pg_dump перед созданием резервной копии."
                );
            }

            // Валидируем путь к pg_dump
            var validationInfo = await pgDumpSettingsService.ValidatePgDumpPathAsync(
                pgDumpSettings.PgDumpPath
            );
            if (!validationInfo.FileExists)
            {
                throw new InvalidOperationException(
                    $"pg_dump не найден по указанному пути: {pgDumpSettings.PgDumpPath}. {validationInfo.Message}"
                );
            }

            var connectionString = GetConnectionString(databaseName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"Не удалось найти строку подключения для базы данных: {databaseName}"
                );
            }

            var backupFileName = $"backup_{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
            var connectionParams = ParseConnectionString(connectionString);

            // Получаем путь для сохранения резервных копий
            var backupPath = await GetBackupPathAsync();
            var tempFilePath = Path.Combine(backupPath, backupFileName);

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pgDumpSettings.PgDumpPath,
                    Arguments = BuildPgDumpArguments(connectionParams, tempFilePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var process = new Process();
                process.StartInfo = processStartInfo;

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var errorMessage = $"Ошибка при создании резервной копии: {error}";
                    logger.LogError(
                        "Ошибка при создании резервной копии: {Error}",
                        error.ToString()
                    );
                    throw new InvalidOperationException(errorMessage);
                }

                // Читаем содержимое временного файла
                var backupContent = await File.ReadAllBytesAsync(tempFilePath);

                // Добавляем файл в MemoryStorage
                var downloadUrl = await MemoryStorage.AddFileAsync(backupFileName, backupContent);

                logger.LogInformation(
                    "Резервная копия создана успешно: {FileName} в {BackupPath}",
                    backupFileName,
                    backupPath
                );
                return downloadUrl;
            }
            finally
            {
                // Файл сохраняется в настраиваемой директории, поэтому не удаляем его
                // Это позволяет пользователю иметь локальные копии резервных копий
                logger.LogDebug(
                    "Резервная копия сохранена в файловой системе: {FilePath}",
                    tempFilePath
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка при создании резервной копии базы данных {DatabaseName}",
                databaseName
            );
            throw;
        }
    }

    /// <summary>
    /// Получает файл резервной копии по имени файла
    /// </summary>
    public async Task<Stream> GetBackupFileAsync(string fileName)
    {
        // Сначала проверяем MemoryStorage
        if (MemoryStorage.FileExists(fileName))
        {
            var (stream, _) = await MemoryStorage.GetFileStreamWithContentTypeAsync(fileName);
            return stream;
        }

        // Затем проверяем настраиваемую директорию
        try
        {
            var backupPath = await GetBackupPathAsync();
            var filePath = Path.Combine(backupPath, fileName);

            if (File.Exists(filePath))
            {
                return new FileStream(filePath, FileMode.Open, FileAccess.Read);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ошибка при получении файла из настраиваемой директории: {FileName}",
                fileName
            );
        }

        throw new FileNotFoundException($"Файл резервной копии не найден: {fileName}");
    }

    /// <summary>
    /// Получает список доступных резервных копий
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableBackupsAsync()
    {
        try
        {
            var backupFiles = new List<string>();

            // Получаем файлы из MemoryStorage
            var memoryFiles = await MemoryStorage.GetAllFileNamesAsync();
            var memoryBackupFiles = memoryFiles
                .Where(f => f.StartsWith("backup_") && f.EndsWith(".sql"))
                .ToList();
            backupFiles.AddRange(memoryBackupFiles);

            // Получаем файлы из настраиваемой директории
            try
            {
                var backupPath = await GetBackupPathAsync();
                if (Directory.Exists(backupPath))
                {
                    var fileSystemFiles = Directory
                        .GetFiles(backupPath, "backup_*.sql")
                        .Select(Path.GetFileName)
                        .Where(f => f != null)
                        .Cast<string>()
                        .ToList();
                    backupFiles.AddRange(fileSystemFiles);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при получении файлов из настраиваемой директории");
            }

            return backupFiles.Distinct().OrderByDescending(f => f).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении списка резервных копий");
            return [];
        }
    }

    /// <summary>
    /// Удаляет старые резервные копии, оставляя только последние N
    /// </summary>
    public async Task<int> CleanupOldBackupsAsync(int keepCount = 10)
    {
        try
        {
            var backupFiles = await GetAvailableBackupsAsync();
            var filesToDelete = backupFiles.Skip(keepCount).ToList();

            var deletedCount = 0;
            foreach (var fileName in filesToDelete)
            {
                try
                {
                    // Удаляем из MemoryStorage
                    if (MemoryStorage.FileExists(fileName))
                    {
                        await MemoryStorage.DeleteFileAsync(fileName);
                        deletedCount++;
                        logger.LogInformation(
                            "Удалена старая резервная копия из MemoryStorage: {FileName}",
                            fileName
                        );
                    }

                    // Удаляем из настраиваемой директории
                    try
                    {
                        var backupPath = await GetBackupPathAsync();
                        var filePath = Path.Combine(backupPath, fileName);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            deletedCount++;
                            logger.LogInformation(
                                "Удалена старая резервная копия из файловой системы: {FileName}",
                                fileName
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Не удалось удалить файл из настраиваемой директории: {FileName}",
                            fileName
                        );
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Не удалось удалить файл: {FileName}", fileName);
                }
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке старых резервных копий");
            return 0;
        }
    }

    /// <summary>
    /// Получает информацию о файле резервной копии
    /// </summary>
    public async Task<BackupFileInfo?> GetBackupFileInfoAsync(string fileName)
    {
        try
        {
            Stream? stream = null;
            var contentType = "application/sql";
            long fileSize = 0;

            // Сначала проверяем MemoryStorage
            if (MemoryStorage.FileExists(fileName))
            {
                var (memoryStream, memoryContentType) =
                    await MemoryStorage.GetFileStreamWithContentTypeAsync(fileName);
                stream = memoryStream;
                contentType = memoryContentType;
                fileSize = stream.Length;
            }
            else
            {
                // Проверяем настраиваемую директорию
                try
                {
                    var backupPath = await GetBackupPathAsync();
                    var filePath = Path.Combine(backupPath, fileName);

                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        fileSize = fileInfo.Length;
                        stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Ошибка при получении информации о файле из настраиваемой директории: {FileName}",
                        fileName
                    );
                }
            }

            if (stream == null)
            {
                return null;
            }

            // Парсим имя файла для получения информации
            var parts = fileName.Split('_');
            if (parts.Length >= 3)
            {
                var databaseName = parts[1];
                var dateTimeStr = parts[2].Replace(".sql", "");

                if (
                    DateTime.TryParseExact(
                        dateTimeStr,
                        "yyyyMMdd_HHmmss",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out var created
                    )
                )
                {
                    return new BackupFileInfo
                    {
                        FileName = fileName,
                        DatabaseName = databaseName,
                        Created = created,
                        Size = fileSize,
                        ContentType = contentType,
                    };
                }
            }

            return new BackupFileInfo
            {
                FileName = fileName,
                DatabaseName = "unknown",
                Created = DateTime.Now,
                Size = fileSize,
                ContentType = contentType,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении информации о файле {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Получает путь для сохранения резервных копий
    /// </summary>
    private async Task<string> GetBackupPathAsync()
    {
        try
        {
            var pgDumpSettings = await pgDumpSettingsService.GetActiveSettingsAsync();
            if (!string.IsNullOrEmpty(pgDumpSettings?.BackupPath))
            {
                // Проверяем, что директория существует или создаем её
                var backupPath = pgDumpSettings.BackupPath.Trim();
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                    logger.LogInformation(
                        "Создана директория для резервных копий: {BackupPath}",
                        backupPath
                    );
                }
                return backupPath;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ошибка при получении пути для резервных копий, используется временная директория"
            );
        }

        // Возвращаем временную директорию по умолчанию
        return Path.GetTempPath();
    }

    /// <summary>
    /// Получает строку подключения для указанной базы данных
    /// </summary>
    private string GetConnectionString(string databaseName)
    {
        var connectionStringKey = databaseName.ToLower() switch
        {
            "dev" => "Dev_Path",
            "prod" => "Prod_Path",
            _ => throw new ArgumentException($"Неподдерживаемая база данных: {databaseName}"),
        };

        var connectionString = configuration.GetConnectionString(connectionStringKey);
        return string.IsNullOrEmpty(connectionString)
            ? throw new InvalidOperationException(
                $"Строка подключения для {connectionStringKey} не найдена в конфигурации"
            )
            : connectionString;
    }

    /// <summary>
    /// Парсит строку подключения PostgreSQL
    /// </summary>
    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var parameters = new Dictionary<string, string>();

        foreach (var param in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = param.Split('=', 2);
            if (keyValue.Length == 2)
            {
                parameters[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }

        return parameters;
    }

    /// <summary>
    /// Строит аргументы для pg_dump
    /// </summary>
    private static string BuildPgDumpArguments(
        Dictionary<string, string> connectionParams,
        string outputPath
    )
    {
        var arguments = new List<string>();

        // Параметры подключения
        if (connectionParams.TryGetValue("Host", out var host))
        {
            arguments.Add($"-h {host}");
        }

        if (connectionParams.TryGetValue("Port", out var port))
        {
            arguments.Add($"-p {port}");
        }

        if (connectionParams.TryGetValue("Database", out var database))
        {
            arguments.Add($"-d {database}");
        }

        if (connectionParams.TryGetValue("User ID", out var userId))
        {
            arguments.Add($"-U {userId}");
        }

        // Параметры вывода
        arguments.Add($"-f \"{outputPath}\"");

        // Дополнительные параметры для лучшего качества резервной копии
        arguments.Add("--verbose");
        arguments.Add("--no-password");

        return string.Join(" ", arguments);
    }
}
