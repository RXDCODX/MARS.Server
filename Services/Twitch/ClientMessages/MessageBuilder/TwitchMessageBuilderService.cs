using MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.DTOs;
using MARS.Server.Services.Twitch.ClientMessages.MessageBuilder.Entitys;
using Microsoft.EntityFrameworkCore;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch.ClientMessages.MessageBuilder;

/// <summary>
/// Сервис для работы с шаблонами сообщений Twitch
/// </summary>
public class TwitchMessageBuilderService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<TwitchMessageBuilderService> _logger;
    private readonly ITwitchClient _twitchClient;
    private readonly IHubContext<TelegramusHub, ITelegramusHub> _hubContext;
    private readonly Dictionary<string, TwitchMessageTemplate> _activeTemplates = new();
    private readonly Dictionary<Guid, DateTime> _lastTriggerTimes = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private const string Channel = TwitchExstension.Channel;

    public TwitchMessageBuilderService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<TwitchMessageBuilderService> logger,
        ITwitchClient twitchClient,
        IHubContext<TelegramusHub, ITelegramusHub> hubContext
    )
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _twitchClient = twitchClient;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Загружаем активные шаблоны при запуске
        await LoadActiveTemplatesAsync();

        // Подписываемся на события сообщений
        _twitchClient.OnMessageReceived += OnMessageReceived;

        // Периодически обновляем кэш шаблонов
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                await LoadActiveTemplatesAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении кэша шаблонов");
            }
        }
    }

    /// <summary>
    /// Обработчик входящих сообщений
    /// </summary>
    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs args)
    {
        if (args.ChatMessage.Channel.Equals(Channel, StringComparison.OrdinalIgnoreCase))
        {
            await ProcessMessageAsync(args.ChatMessage);
        }
    }

    /// <summary>
    /// Обработка сообщения и поиск подходящих шаблонов
    /// </summary>
    private async Task ProcessMessageAsync(ChatMessage chatMessage)
    {
        try
        {
            var message = chatMessage.Message.ToLowerInvariant();
            var username = chatMessage.Username;
            var displayName = chatMessage.DisplayName;

            // Ищем подходящие шаблоны
            var matchingTemplates = _activeTemplates
                .Values.Where(t => message.Contains(t.TriggerWord.ToLowerInvariant()))
                .Where(t => IsTemplateReady(t.Id))
                .Where(t => ShouldTriggerRandomly(t))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList();

            if (!matchingTemplates.Any())
                return;

            // Выбираем первый подходящий шаблон (с наивысшим приоритетом)
            var selectedTemplate = matchingTemplates.First();

            // Генерируем и отправляем сообщение
            await SendTemplateMessageAsync(selectedTemplate, chatMessage);

            // Обновляем статистику использования
            await UpdateTemplateUsageAsync(selectedTemplate.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при обработке сообщения от {Username}",
                chatMessage.Username
            );
        }
    }

    /// <summary>
    /// Проверяет, готов ли шаблон к использованию (не истек ли кулдаун)
    /// </summary>
    private bool IsTemplateReady(Guid templateId)
    {
        if (!_lastTriggerTimes.TryGetValue(templateId, out var lastTrigger))
            return true;

        var template = _activeTemplates.Values.FirstOrDefault(t => t.Id == templateId);
        if (template == null)
            return false;

        return DateTime.UtcNow >= lastTrigger.AddSeconds(template.CooldownSeconds);
    }

    /// <summary>
    /// Проверяет, должен ли шаблон сработать с учетом случайности
    /// </summary>
    private bool ShouldTriggerRandomly(TwitchMessageTemplate template)
    {
        if (template.RandomChance >= 100)
            return true;
        if (template.RandomChance <= 0)
            return false;

        var random = Random.Shared.Next(1, 101);
        return random <= template.RandomChance;
    }

    /// <summary>
    /// Отправляет сообщение на основе шаблона
    /// </summary>
    private async Task SendTemplateMessageAsync(
        TwitchMessageTemplate template,
        ChatMessage chatMessage
    )
    {
        try
        {
            var processedMessage = ProcessMessageTemplate(template, chatMessage);

            // Отправляем сообщение в чат
            _twitchClient.SendMessage(Channel, processedMessage);

            // Отправляем через SignalR для OBS
            await _hubContext.Clients.All.TemplateMessage(processedMessage);

            // Обновляем время последнего срабатывания
            _lastTriggerTimes[template.Id] = DateTime.UtcNow;

            _logger.LogInformation(
                "Отправлено сообщение по шаблону '{TemplateName}' для пользователя {Username}",
                template.Name,
                chatMessage.Username
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при отправке сообщения по шаблону {TemplateName}",
                template.Name
            );
        }
    }

    /// <summary>
    /// Обрабатывает шаблон сообщения, заменяя переменные
    /// </summary>
    private string ProcessMessageTemplate(TwitchMessageTemplate template, ChatMessage chatMessage)
    {
        var message = template.MessageTemplate;
        var username = chatMessage.Username;
        var displayName = chatMessage.DisplayName;

        // Заменяем переменные в шаблоне
        message = message.Replace("{user}", username, StringComparison.OrdinalIgnoreCase);
        message = message.Replace("{displayName}", displayName, StringComparison.OrdinalIgnoreCase);
        message = message.Replace("{username}", username, StringComparison.OrdinalIgnoreCase);

        // Добавляем цвет автора, если указан
        if (!string.IsNullOrEmpty(template.AuthorColor))
        {
            var authorName = template.AuthorName ?? displayName;
            message = $"{authorName}: {message}";
        }

        return message;
    }

    /// <summary>
    /// Загружает активные шаблоны из базы данных
    /// </summary>
    private async Task LoadActiveTemplatesAsync()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var templates = await dbContext
                .TwitchMessageTemplates.AsNoTracking()
                .Where(t => t.IsActive)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync();

            await _semaphore.WaitAsync();
            try
            {
                _activeTemplates.Clear();
                foreach (var template in templates)
                {
                    _activeTemplates[template.TriggerWord.ToLowerInvariant()] = template;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            _logger.LogInformation(
                "Загружено {Count} активных шаблонов сообщений",
                templates.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке активных шаблонов");
        }
    }

    /// <summary>
    /// Обновляет статистику использования шаблона
    /// </summary>
    private async Task UpdateTemplateUsageAsync(Guid templateId)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var template = await dbContext.TwitchMessageTemplates.FindAsync(templateId);
            if (template != null)
            {
                template.UsageCount++;
                template.LastTriggeredAt = DateTime.UtcNow;
                template.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ошибка при обновлении статистики использования шаблона {TemplateId}",
                templateId
            );
        }
    }

    /// <summary>
    /// Создает новый шаблон сообщения
    /// </summary>
    public async Task<TwitchMessageTemplateResponseDto> CreateTemplateAsync(
        CreateTwitchMessageTemplateDto dto
    )
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var template = new TwitchMessageTemplate
            {
                Name = dto.Name,
                MessageTemplate = dto.MessageTemplate,
                Description = dto.Description,
                TriggerWord = dto.TriggerWord,
                AuthorColor = dto.AuthorColor,
                AuthorName = dto.AuthorName,
                Priority = dto.Priority,
                RandomChance = dto.RandomChance,
                CooldownSeconds = dto.CooldownSeconds,
            };

            dbContext.TwitchMessageTemplates.Add(template);
            await dbContext.SaveChangesAsync();

            // Обновляем кэш
            await LoadActiveTemplatesAsync();

            return MapToResponseDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании шаблона сообщения");
            throw;
        }
    }

    /// <summary>
    /// Обновляет существующий шаблон
    /// </summary>
    public async Task<TwitchMessageTemplateResponseDto?> UpdateTemplateAsync(
        Guid id,
        UpdateTwitchMessageTemplateDto dto
    )
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var template = await dbContext.TwitchMessageTemplates.FindAsync(id);
            if (template == null)
                return null;

            // Обновляем только указанные поля
            if (dto.Name != null)
                template.Name = dto.Name;
            if (dto.MessageTemplate != null)
                template.MessageTemplate = dto.MessageTemplate;
            if (dto.Description != null)
                template.Description = dto.Description;
            if (dto.TriggerWord != null)
                template.TriggerWord = dto.TriggerWord;
            if (dto.AuthorColor != null)
                template.AuthorColor = dto.AuthorColor;
            if (dto.AuthorName != null)
                template.AuthorName = dto.AuthorName;
            if (dto.IsActive.HasValue)
                template.IsActive = dto.IsActive.Value;
            if (dto.Priority.HasValue)
                template.Priority = dto.Priority.Value;
            if (dto.RandomChance.HasValue)
                template.RandomChance = dto.RandomChance.Value;
            if (dto.CooldownSeconds.HasValue)
                template.CooldownSeconds = dto.CooldownSeconds.Value;

            template.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            // Обновляем кэш
            await LoadActiveTemplatesAsync();

            return MapToResponseDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении шаблона {TemplateId}", id);
            throw;
        }
    }

    /// <summary>
    /// Удаляет шаблон
    /// </summary>
    public async Task<bool> DeleteTemplateAsync(Guid id)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var template = await dbContext.TwitchMessageTemplates.FindAsync(id);
            if (template == null)
                return false;

            dbContext.TwitchMessageTemplates.Remove(template);
            await dbContext.SaveChangesAsync();

            // Обновляем кэш
            await LoadActiveTemplatesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении шаблона {TemplateId}", id);
            throw;
        }
    }

    /// <summary>
    /// Получает все шаблоны
    /// </summary>
    public async Task<List<TwitchMessageTemplateResponseDto>> GetAllTemplatesAsync()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var templates = await dbContext
                .TwitchMessageTemplates.AsNoTracking()
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync();

            return templates.Select(MapToResponseDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении всех шаблонов");
            throw;
        }
    }

    /// <summary>
    /// Получает шаблон по ID
    /// </summary>
    public async Task<TwitchMessageTemplateResponseDto?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var template = await dbContext
                .TwitchMessageTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            return template != null ? MapToResponseDto(template) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении шаблона {TemplateId}", id);
            throw;
        }
    }

    /// <summary>
    /// Маппинг в DTO ответа
    /// </summary>
    private static TwitchMessageTemplateResponseDto MapToResponseDto(TwitchMessageTemplate template)
    {
        return new TwitchMessageTemplateResponseDto
        {
            Id = template.Id,
            Name = template.Name,
            MessageTemplate = template.MessageTemplate,
            Description = template.Description,
            TriggerWord = template.TriggerWord,
            AuthorColor = template.AuthorColor,
            AuthorName = template.AuthorName,
            IsActive = template.IsActive,
            Priority = template.Priority,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            UsageCount = template.UsageCount,
            RandomChance = template.RandomChance,
            CooldownSeconds = template.CooldownSeconds,
            LastTriggeredAt = template.LastTriggeredAt,
        };
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
    }
}
