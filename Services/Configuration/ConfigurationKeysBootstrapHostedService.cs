using System.Collections;
using System.Reflection;
using MARS.Server.ApplicationState;
using MARS.Server.DataBaseContext;
using Microsoft.EntityFrameworkCore;
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
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(
                cancellationToken
            );

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
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
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
            .Where(field =>
                field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string)
            )
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
            RootStateKeys.TtsFilterEnabled => new RootState
            {
                Name = key,
                Value = true.ToString(),
                Description = "Флаг включения фильтра дубликатов TTS сообщений",
                TypeDescription = "bool",
            },
            RootStateKeys.WaifuRollCooldownMinutes => new RootState
            {
                Name = key,
                Value = 20L.ToString(),
                Description = "Кулдаун ролла вайфу в минутах",
                TypeDescription = "long",
            },
            RootStateKeys.TwitchFumoFridayNightVideoPath => new RootState
            {
                Name = key,
                Value = "wwwroot/Alerts/fumoFridayNight.webm",
                Description = "Путь до видео для Fumo Friday Night",
                TypeDescription = "string",
            },
            RootStateKeys.RandomRewardCooldownSeconds => new RootState
            {
                Name = key,
                Value = 60L.ToString(),
                Description = "Кулдаун награды RandomReward для одного пользователя в секундах",
                TypeDescription = "long",
            },
            RootStateKeys.SoundRequestProvider => new RootState
            {
                Name = key,
                Value = "YouTube",
                Description = "Активный провайдер SoundRequest (YouTube/Spotify)",
                TypeDescription = "enum: SoundRequestProvider",
            },
            RootStateKeys.DiscordTtsRelayTargetUserId => new RootState
            {
                Name = key,
                Value = 260383142903414785UL.ToString(),
                Description = "ID Discord пользователя для TTS voice relay",
                TypeDescription = "ulong",
            },
            RootStateKeys.DiscordTtsRelayTargetVoiceChannelId => new RootState
            {
                Name = key,
                Value = 1406679380369080481UL.ToString(),
                Description = "ID Discord голосового канала для TTS voice relay",
                TypeDescription = "ulong",
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
