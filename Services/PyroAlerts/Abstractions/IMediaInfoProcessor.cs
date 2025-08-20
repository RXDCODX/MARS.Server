using MARS.Server.Services.PyroAlerts.Entitys;

namespace MARS.Server.Services.PyroAlerts.Abstractions;

/// <summary>
/// Интерфейс для обработки медиа-информации из различных источников
/// </summary>
public interface IMediaInfoProcessor
{
    /// <summary>
    /// Создает объект MediaInfo из Telegram сообщения
    /// </summary>
    /// <param name="client">Клиент Telegram бота</param>
    /// <param name="message">Сообщение Telegram</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Обработанная медиа-информация или null при ошибке</returns>
    Task<MediaInfo?> ProcessTelegramMessageAsync(
        ITelegramBotClient client, 
        Message message, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создает объект MediaInfo для голосового сообщения с фотографией чата
    /// </summary>
    /// <param name="client">Клиент Telegram бота</param>
    /// <param name="message">Голосовое сообщение</param>
    /// <param name="chat">Информация о чате</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Обработанная медиа-информация или null при ошибке</returns>
    Task<MediaInfo?> ProcessVoiceMessageWithChatPhotoAsync(
        ITelegramBotClient client, 
        Message message, 
        ChatFullInfo chat, 
        CancellationToken cancellationToken = default);
}