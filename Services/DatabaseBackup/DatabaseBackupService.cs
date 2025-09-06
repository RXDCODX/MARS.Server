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

            // Создаем временный файл для pg_dump
            var tempFilePath = Path.GetTempFileName();

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
                    "Резервная копия создана успешно: {FileName}",
                    backupFileName
                );
                return downloadUrl;
            }
            finally
            {
                // Удаляем временный файл
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Не удалось удалить временный файл: {TempFilePath}",
                            tempFilePath
                        );
                    }
                }
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
        if (!MemoryStorage.FileExists(fileName))
        {
            throw new FileNotFoundException($"Файл резервной копии не найден: {fileName}");
        }

        var (stream, _) = await MemoryStorage.GetFileStreamWithContentTypeAsync(fileName);
        return stream;
    }

    /// <summary>
    /// Получает список доступных резервных копий
    /// </summary>
    public async Task<IEnumerable<string>> GetAvailableBackupsAsync()
    {
        try
        {
            var allFiles = await MemoryStorage.GetAllFileNamesAsync();
            var backupFiles = allFiles
                .Where(f => f.StartsWith("backup_") && f.EndsWith(".sql"))
                .OrderByDescending(f => f)
                .ToList();

            return backupFiles;
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
                    await MemoryStorage.DeleteFileAsync(fileName);
                    deletedCount++;
                    logger.LogInformation("Удалена старая резервная копия: {FileName}", fileName);
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
            if (!MemoryStorage.FileExists(fileName))
            {
                return null;
            }

            var (stream, contentType) = await MemoryStorage.GetFileStreamWithContentTypeAsync(
                fileName
            );

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
                        Size = stream.Length,
                        ContentType = contentType,
                    };
                }
            }

            return new BackupFileInfo
            {
                FileName = fileName,
                DatabaseName = "unknown",
                Created = DateTime.Now,
                Size = stream.Length,
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
