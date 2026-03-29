using System.Collections;
using System.Reflection;
using MARS.Server.ApplicationState;
using EnvironmentVariableEntity = MARS.Server.Services.EnvironmentVariable.Entitys.EnvironmentVariable;

namespace MARS.Server.Services.Configuration;

public sealed class ConfigurationKeysBootstrapHostedService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<ConfigurationKeysBootstrapHostedService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            await EnsureRootStateKeysAsync(dbContext, cancellationToken);
            await EnsureEnvironmentVariableKeysAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при автоинициализации ключей конфигурации");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var result = Task.CompletedTask;
        return result;
    }

    private static async Task EnsureRootStateKeysAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var knownKeys = GetRootStateKeys();
        var existingKeys = await dbContext
            .RootState.AsNoTracking()
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var existingKeysHash = existingKeys.ToHashSet(StringComparer.Ordinal);

        var missingStates = knownKeys
            .Where(key => !existingKeysHash.Contains(key))
            .Select(CreateDefaultRootState)
            .ToList();

        if (missingStates.Count > 0)
        {
            await dbContext.RootState.AddRangeAsync(missingStates, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureEnvironmentVariableKeysAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var existingKeys = await dbContext
            .EnvironmentVariables.AsNoTracking()
            .Select(e => e.Key)
            .ToListAsync(cancellationToken);
        var existingKeysHash = existingKeys.ToHashSet(StringComparer.Ordinal);

        var environmentVariables = Environment.GetEnvironmentVariables();
        var missingVariables = new List<EnvironmentVariableEntity>();

        foreach (DictionaryEntry environmentVariable in environmentVariables)
        {
            var key = environmentVariable.Key?.ToString();
            var value = environmentVariable.Value?.ToString();

            if (!string.IsNullOrWhiteSpace(key) && !existingKeysHash.Contains(key))
            {
                missingVariables.Add(
                    new EnvironmentVariableEntity
                    {
                        Key = key,
                        Value = value,
                        Description = "Автосоздано из переменных окружения",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    }
                );

                existingKeysHash.Add(key);
            }
        }

        if (missingVariables.Count > 0)
        {
            await dbContext.EnvironmentVariables.AddRangeAsync(missingVariables, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<string> GetRootStateKeys()
    {
        var result = typeof(RootStateKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => field.GetRawConstantValue()?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return result;
    }

    private static RootState CreateDefaultRootState(string key)
    {
        var result = key switch
        {
            RootStateKeys.RandomMemeOnlineIsStop => new RootState
            {
                Name = key,
                Value = false.ToString(),
                Description = "Флаг остановки сервиса RandomMemeOnline",
                TypeDescription = "bool",
            },
            RootStateKeys.PuntoSwitcherFilterEnabled => new RootState
            {
                Name = key,
                Value = true.ToString(),
                Description = "Флаг включения фильтра PuntoSwitcher",
                TypeDescription = "bool",
            },
            RootStateKeys.WaifuRollCooldownMinutes => new RootState
            {
                Name = key,
                Value = 20L.ToString(),
                Description = "Кулдаун ролла вайфу в минутах",
                TypeDescription = "long",
            },
            RootStateKeys.SoundRequestProvider => new RootState
            {
                Name = key,
                Value = "YouTube",
                Description = "Активный провайдер SoundRequest (YouTube/Spotify)",
                TypeDescription = "enum: SoundRequestProvider",
            },
            _ => new RootState
            {
                Name = key,
                Value = string.Empty,
                Description = "Автосозданный ключ RootState",
                TypeDescription = "string",
            },
        };

        return result;
    }
}