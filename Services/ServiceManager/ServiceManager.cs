using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MARS.Server.Services.ServiceManager.Entitys;

namespace MARS.Server.Services.ServiceManager;

/// <summary>
/// Реализация менеджера сервисов
/// </summary>
public class ServiceManager : IServiceManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceManager> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ConcurrentDictionary<string, ManagedServiceBase> _managedServices = new();
    private readonly ConcurrentDictionary<string, IHostedService> _hostedServices = new();

    public ServiceManager(
        IServiceProvider serviceProvider,
        ILogger<ServiceManager> logger,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        InitializeServices();
    }

    private void InitializeServices()
    {
        // Получаем все зарегистрированные IHostedService
        var hostedServices = _serviceProvider.GetServices<IHostedService>();

        foreach (var service in hostedServices)
        {
            if (service is ManagedServiceBase managedService)
            {
                _managedServices[managedService.ServiceName] = managedService;
                _logger.LogInformation(
                    "Registered managed service: {ServiceName}",
                    managedService.ServiceName
                );
            }
            else
            {
                // Для обычных IHostedService создаем обертку
                var serviceName = GetServiceName(service);
                _hostedServices[serviceName] = service;
                _logger.LogInformation("Registered hosted service: {ServiceName}", serviceName);
            }
        }

        // Инициализируем состояния в базе данных
        InitializeServiceStatesAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeServiceStatesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Получаем все существующие состояния
        var existingStates = await dbContext.ServiceStates.ToDictionaryAsync(s => s.ServiceName);

        // Обрабатываем управляемые сервисы
        foreach (var service in _managedServices.Values)
        {
            if (!existingStates.TryGetValue(service.ServiceName, out var state))
            {
                // Создаем новое состояние
                state = new ServiceState
                {
                    ServiceName = service.ServiceName,
                    DisplayName = service.DisplayName,
                    Description = service.Description,
                    IsServiceActive = service.IsServiceActive,
                    Status = service.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                dbContext.ServiceStates.Add(state);
                _logger.LogInformation(
                    "Created new service state for: {ServiceName}",
                    service.ServiceName
                );
            }
            else
            {
                // Обновляем существующее состояние
                state.DisplayName = service.DisplayName;
                state.Description = service.Description;
                state.IsServiceActive = service.IsServiceActive;
                state.Status = service.Status;
                state.UpdatedAt = DateTime.UtcNow;

                // Применяем сохраненное состояние к сервису
                service.IsServiceActive = state.IsServiceActive;

                _logger.LogInformation(
                    "Updated service state for: {ServiceName}",
                    service.ServiceName
                );

                // Загружаем состояние в сервис
                await service.LoadStateAsync(state);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static string GetServiceName(IHostedService service)
    {
        var type = service.GetType();

        // Пытаемся получить имя из атрибутов или типа
        var serviceName =
            type.GetCustomAttribute<ServiceNameAttribute>()?.Name
            ?? type.Name.Replace("Service", "").Replace("Worker", "").Replace("Manager", "");

        return serviceName.ToLowerInvariant();
    }

    public Task<Dictionary<string, ServiceStatus>> GetServicesStatusAsync()
    {
        var statuses = new Dictionary<string, ServiceStatus>();

        // Управляемые сервисы
        foreach (var service in _managedServices)
        {
            statuses[service.Key] = service.Value.Status;
        }

        // Обычные hosted сервисы
        foreach (var service in _hostedServices)
        {
            // Для обычных сервисов определяем статус по типу
            statuses[service.Key] = ServiceStatus.Running; // По умолчанию считаем запущенными
        }

        return Task.FromResult(statuses);
    }

    public async Task<bool> StartServiceAsync(string serviceName)
    {
        try
        {
            _logger.LogInformation("Attempting to start service: {ServiceName}", serviceName);

            if (_managedServices.TryGetValue(serviceName, out var managedService))
            {
                await managedService.StartAsync();

                // Обновляем состояние в БД
                await UpdateServiceStateAsync(serviceName, managedService);

                return true;
            }

            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service: {ServiceName}", serviceName);
            return false;
        }
    }

    public async Task<bool> StopServiceAsync(string serviceName)
    {
        try
        {
            _logger.LogInformation("Attempting to stop service: {ServiceName}", serviceName);

            if (_managedServices.TryGetValue(serviceName, out var managedService))
            {
                await managedService.StopAsync();

                // Обновляем состояние в БД
                await UpdateServiceStateAsync(serviceName, managedService);

                return true;
            }

            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service: {ServiceName}", serviceName);
            return false;
        }
    }

    public async Task<bool> RestartServiceAsync(string serviceName)
    {
        var stopped = await StopServiceAsync(serviceName);
        if (!stopped)
        {
            return false;
        }

        await Task.Delay(2000); // Пауза между остановкой и запуском
        return await StartServiceAsync(serviceName);
    }

    public Task<ServiceInfo?> GetServiceInfoAsync(string serviceName)
    {
        if (_managedServices.TryGetValue(serviceName, out var managedService))
        {
            return Task.FromResult<ServiceInfo?>(managedService.GetServiceInfo());
        }

        // Для обычных hosted сервисов создаем базовую информацию
        return _hostedServices.TryGetValue(serviceName, out _)
            ? Task.FromResult<ServiceInfo?>(
                new ServiceInfo
                {
                    Name = serviceName,
                    DisplayName = GetServiceDisplayName(serviceName),
                    Description = GetServiceDescription(serviceName),
                    Status = ServiceStatus.Running, // По умолчанию
                    IsEnabled = true,
                }
            )
            : Task.FromResult<ServiceInfo?>(null);
    }

    public async Task<IEnumerable<ServiceLog>> GetServiceLogsAsync(
        string serviceName,
        int count = 100
    )
    {
        // Здесь будет логика получения логов из базы данных или файлов
        // Пока что возвращаем заглушку
        var logs = new List<ServiceLog>();

        for (var i = 0; i < Math.Min(count, 10); i++)
        {
            logs.Add(
                new ServiceLog
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    Level =
                        i % 3 == 0 ? "Error"
                        : i % 2 == 0 ? "Warning"
                        : "Info",
                    Message = $"Log message {i} for service {serviceName}",
                    Exception = i % 5 == 0 ? $"Exception {i}" : null,
                }
            );
        }

        return await Task.FromResult(logs);
    }

    public async Task<bool> SetServiceActiveAsync(string serviceName, bool isActive)
    {
        try
        {
            if (_managedServices.TryGetValue(serviceName, out var managedService))
            {
                managedService.IsServiceActive = isActive;

                // Обновляем состояние в БД
                await UpdateServiceStateAsync(serviceName, managedService);

                _logger.LogInformation(
                    "Service {ServiceName} active state set to {IsServiceActive}",
                    serviceName,
                    isActive
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set service {ServiceName} active state to {IsServiceActive}",
                serviceName,
                isActive
            );
            return false;
        }
    }

    public Task<IEnumerable<ServiceInfo>> GetAllServicesAsync()
    {
        var services = _managedServices.Values.Select(service => service.GetServiceInfo()).ToList();

        // Управляемые сервисы

        // Обычные hosted сервисы
        services.AddRange(
            _hostedServices.Select(service => new ServiceInfo
            {
                Name = service.Key,
                DisplayName = GetServiceDisplayName(service.Key),
                Description = GetServiceDescription(service.Key),
                Status = ServiceStatus.Running,
                IsEnabled = true,
            })
        );

        return Task.FromResult<IEnumerable<ServiceInfo>>(services);
    }

    private async Task UpdateServiceStateAsync(string serviceName, ManagedServiceBase service)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var state = await dbContext.ServiceStates.FirstOrDefaultAsync(s =>
                s.ServiceName == serviceName
            );
            if (state != null)
            {
                state.Status = service.Status;
                state.IsServiceActive = service.IsServiceActive;
                state.LastStartTime = service.StartTime;
                state.LastActivity = service.LastActivity;
                state.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update service state for {ServiceName}", serviceName);
        }
    }

    private static string GetServiceDisplayName(string serviceName)
    {
        var displayNames = new Dictionary<string, string>
        {
            ["twitch-auth"] = "Twitch Authentication",
            ["twitch-auto-messages"] = "Auto Messages",
            ["twitch-fumo-friday"] = "Fumo Friday",
            ["twitch-hello-videos"] = "Hello Videos",
            ["twitch-media-alerts"] = "Media Alerts",
            ["twitch-mini-games"] = "Mini Games",
            ["twitch-synthesizer"] = "Synthesizer",
            ["twitch-waifu-rolls"] = "Waifu Rolls",
            ["twitch-sound-request"] = "Sound Request",
            ["twitch-clip-creator"] = "Clip Creator",
            ["twitch-frame-data"] = "Frame Data",
            ["twitch-messages-hub"] = "Messages Hub",
            ["twitch-screen-particles"] = "Screen Particles",
            ["twitch-rewards"] = "Rewards",
            ["random-meme-worker"] = "Random Meme Worker",
            ["random-meme-online"] = "Random Meme Online",
            ["sound-request-backend"] = "Sound Request Backend",
            ["sound-request-playlist"] = "Sound Request Playlist",
            ["pyro-alerts"] = "Pyro Alerts",
            ["waifu-roll"] = "Waifu Roll",
            ["shikimori"] = "Shikimori",
            ["365-genius"] = "365 Genius",
            ["honkai"] = "Honkai",
            ["telegram-bot"] = "Telegram Bot",
        };

        return displayNames.GetValueOrDefault(serviceName, serviceName);
    }

    private static string GetServiceDescription(string serviceName)
    {
        var descriptions = new Dictionary<string, string>
        {
            ["twitch-auth"] = "Сервис аутентификации Twitch",
            ["twitch-auto-messages"] = "Автоматические сообщения в Twitch",
            ["twitch-fumo-friday"] = "Сервис Fumo Friday",
            ["twitch-hello-videos"] = "Приветственные видео",
            ["twitch-media-alerts"] = "Медиа алерты",
            ["twitch-mini-games"] = "Мини-игры",
            ["twitch-synthesizer"] = "Синтезатор речи",
            ["twitch-waifu-rolls"] = "Вайфу роллы",
            ["twitch-sound-request"] = "Запросы звуков",
            ["twitch-clip-creator"] = "Создание клипов",
            ["twitch-frame-data"] = "Данные кадров Tekken",
            ["twitch-messages-hub"] = "Хаб сообщений",
            ["twitch-screen-particles"] = "Частицы на экране",
            ["twitch-rewards"] = "Награды Twitch",
            ["random-meme-worker"] = "Рабочий случайных мемов",
            ["random-meme-online"] = "Онлайн случайных мемов",
            ["sound-request-backend"] = "Бэкенд звуковых запросов",
            ["sound-request-playlist"] = "Плейлист звуковых запросов",
            ["pyro-alerts"] = "Алерты Pyro",
            ["waifu-roll"] = "Сервис вайфу роллов",
            ["shikimori"] = "Сервис Shikimori",
            ["365-genius"] = "Сервис 365 Genius",
            ["honkai"] = "Сервис Honkai",
            ["telegram-bot"] = "Telegram бот",
        };

        return descriptions.GetValueOrDefault(serviceName, "Описание недоступно");
    }
}
