using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.DTOs;

namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Interfaces;

public interface IAutoMessagesService
{
    /// <summary>
    /// Получить все автоматические сообщения
    /// </summary>
    Task<IEnumerable<AutoMessageDto>> GetAllAutoMessagesAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить автоматическое сообщение по ID
    /// </summary>
    Task<AutoMessageDto?> GetAutoMessageByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Создать новое автоматическое сообщение
    /// </summary>
    Task<AutoMessageDto> CreateAutoMessageAsync(
        CreateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Обновить автоматическое сообщение
    /// </summary>
    Task<AutoMessageDto?> UpdateAutoMessageAsync(
        Guid id,
        UpdateAutoMessageRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Удалить автоматическое сообщение
    /// </summary>
    Task<bool> DeleteAutoMessageAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
}
