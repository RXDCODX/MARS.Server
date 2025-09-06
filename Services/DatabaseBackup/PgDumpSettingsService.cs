using System.Diagnostics;
using System.Text;
using MARS.Server.DataBaseContext;
using MARS.Server.Services.DatabaseBackup.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.Services.DatabaseBackup;

/// <summary>
/// Сервис для управления настройками pg_dump
/// </summary>
public class PgDumpSettingsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<PgDumpSettingsService> logger
) : IPgDumpSettingsService
{
    /// <summary>
    /// Получает активные настройки pg_dump
    /// </summary>
    public async Task<PgDumpSettings?> GetActiveSettingsAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            return await dbContext
                .PgDumpSettings.Where(s => s.IsActive)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении активных настроек pg_dump");
            return null;
        }
    }

    /// <summary>
    /// Обновляет настройки pg_dump
    /// </summary>
    public async Task<PgDumpSettings> UpdateSettingsAsync(UpdatePgDumpSettingsRequest request)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Деактивируем все существующие настройки
            var existingSettings = await dbContext
                .PgDumpSettings.Where(s => s.IsActive)
                .ToListAsync();

            foreach (var setting in existingSettings)
            {
                setting.IsActive = false;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            // Создаем новые настройки
            var newSettings = new PgDumpSettings
            {
                PgDumpPath = request.PgDumpPath.Trim(),
                Comment = request.Comment?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            dbContext.PgDumpSettings.Add(newSettings);
            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Настройки pg_dump обновлены: {PgDumpPath}",
                newSettings.PgDumpPath
            );

            return newSettings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении настроек pg_dump");
            throw;
        }
    }

    /// <summary>
    /// Валидирует путь к pg_dump
    /// </summary>
    public async Task<PgDumpValidationInfo> ValidatePgDumpPathAsync(string pgDumpPath)
    {
        var validationInfo = new PgDumpValidationInfo { LastChecked = DateTime.UtcNow };

        try
        {
            if (string.IsNullOrWhiteSpace(pgDumpPath))
            {
                validationInfo.Message = "Путь к pg_dump не указан";
                return validationInfo;
            }

            // Проверяем существование файла
            if (!File.Exists(pgDumpPath))
            {
                validationInfo.Message = $"Файл pg_dump не найден по пути: {pgDumpPath}";
                return validationInfo;
            }

            validationInfo.FileExists = true;

            // Проверяем версию pg_dump
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = "--version",
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

                if (process.ExitCode == 0)
                {
                    validationInfo.Version = output.ToString().Trim();
                    validationInfo.Message = "pg_dump найден и работает корректно";
                }
                else
                {
                    validationInfo.Message = $"Ошибка при проверке версии pg_dump: {error}";
                }
            }
            catch (Exception ex)
            {
                validationInfo.Message = $"Ошибка при проверке версии pg_dump: {ex.Message}";
                logger.LogWarning(
                    ex,
                    "Ошибка при проверке версии pg_dump: {PgDumpPath}",
                    pgDumpPath
                );
            }
        }
        catch (Exception ex)
        {
            validationInfo.Message = $"Ошибка при валидации пути: {ex.Message}";
            logger.LogError(ex, "Ошибка при валидации пути pg_dump: {PgDumpPath}", pgDumpPath);
        }

        return validationInfo;
    }

    /// <summary>
    /// Получает историю настроек pg_dump
    /// </summary>
    public async Task<IEnumerable<PgDumpSettings>> GetSettingsHistoryAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            return await dbContext.PgDumpSettings.OrderByDescending(s => s.UpdatedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении истории настроек pg_dump");
            return [];
        }
    }

    /// <summary>
    /// Проверяет, настроены ли настройки pg_dump
    /// </summary>
    public async Task<bool> IsConfiguredAsync()
    {
        try
        {
            var activeSettings = await GetActiveSettingsAsync();
            if (activeSettings == null)
            {
                return false;
            }

            var validationInfo = await ValidatePgDumpPathAsync(activeSettings.PgDumpPath);
            return validationInfo.FileExists && !string.IsNullOrEmpty(validationInfo.Version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при проверке конфигурации pg_dump");
            return false;
        }
    }
}
