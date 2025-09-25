using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.DTOs;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Interfaces;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Services;

public class AutoMessagesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<AutoMessagesService> logger
) : IAutoMessagesService
{
    public async Task<IEnumerable<AutoMessageDto>> GetAllAutoMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = new List<AutoMessageDto>();

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var messages = await dbContext
                .AutoMessages.AsNoTracking()
                .OrderBy(m => m.Message)
                .ToListAsync(cancellationToken);

            result = messages.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении всех автоматических сообщений");
        }

        return result;
    }

    public async Task<AutoMessageDto?> GetAutoMessageByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        AutoMessageDto? result = null;

        if (id != Guid.Empty)
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var message = await dbContext
                    .AutoMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                result = message != null ? MapToDto(message) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении автоматического сообщения по ID: {Id}", id);
            }
        }

        return result;
    }

    public async Task<AutoMessageDto> CreateAutoMessageAsync(
        CreateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = new AutoMessageDto
        {
            Id = Guid.Empty,
            Message = string.Empty
        };

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

                var autoMessage = new AutoMessage
                {
                    Message = request.Message.Trim()
                };

                dbContext.AutoMessages.Add(autoMessage);
                await dbContext.SaveChangesAsync(cancellationToken);

                result = MapToDto(autoMessage);

                logger.LogInformation(
                    "Создано новое автоматическое сообщение с ID: {Id}",
                    autoMessage.Id
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при создании автоматического сообщения");
            }
        }
        else
        {
            logger.LogWarning("Попытка создать автоматическое сообщение с пустым текстом");
        }

        return result;
    }

    public async Task<AutoMessageDto?> UpdateAutoMessageAsync(
        Guid id,
        UpdateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        AutoMessageDto? result = null;

        if (id != Guid.Empty && !string.IsNullOrWhiteSpace(request.Message))
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var autoMessage = await dbContext
                    .AutoMessages
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (autoMessage != null)
                {
                    autoMessage.Message = request.Message.Trim();
                    await dbContext.SaveChangesAsync(cancellationToken);

                    result = MapToDto(autoMessage);

                    logger.LogInformation(
                        "Обновлено автоматическое сообщение с ID: {Id}",
                        autoMessage.Id
                    );
                }
                else
                {
                    logger.LogWarning("Автоматическое сообщение с ID {Id} не найдено", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при обновлении автоматического сообщения с ID: {Id}", id);
            }
        }
        else
        {
            logger.LogWarning(
                "Попытка обновить автоматическое сообщение с некорректными данными. ID: {Id}, Message: {Message}",
                id,
                request.Message
            );
        }

        return result;
    }

    public async Task<bool> DeleteAutoMessageAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var result = false;

        if (id != Guid.Empty)
        {
            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var autoMessage = await dbContext
                    .AutoMessages
                    .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (autoMessage != null)
                {
                    dbContext.AutoMessages.Remove(autoMessage);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    result = true;

                    logger.LogInformation(
                        "Удалено автоматическое сообщение с ID: {Id}",
                        autoMessage.Id
                    );
                }
                else
                {
                    logger.LogWarning("Автоматическое сообщение с ID {Id} не найдено", id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при удалении автоматического сообщения с ID: {Id}", id);
            }
        }
        else
        {
            logger.LogWarning("Попытка удалить автоматическое сообщение с пустым ID");
        }

        return result;
    }

    private static AutoMessageDto MapToDto(AutoMessage autoMessage)
    {
        return new AutoMessageDto
        {
            Id = autoMessage.Id,
            Message = autoMessage.Message
        };
    }
}
