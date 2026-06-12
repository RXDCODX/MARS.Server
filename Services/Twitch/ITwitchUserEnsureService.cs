using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Services.Twitch.Entitys;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace MARS.Server.Services.Twitch;

/// <summary>
/// Интерфейс для сервиса гарантированного создания/получения пользователей Twitch в БД
/// </summary>
public interface ITwitchUserEnsureService
{
    /// <summary>
    /// Гарантирует наличие пользователя в БД из ChatMessage.
    /// </summary>
    /// <param name="chatMessage">Сообщение из чата с информацией о пользователе</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    Task<TwitchUser> EnsureUserExistsAsync(
        ChatMessage chatMessage,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Гарантирует наличие пользователя в БД из OnMessageReceivedArgs.
    /// </summary>
    /// <param name="args">Аргументы сообщения из чата</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    Task<TwitchUser> EnsureUserExistsAsync(
        OnMessageReceivedArgs args,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Гарантирует наличие пользователя в БД из ChannelPointsCustomRewardRedemptionArgs.
    /// </summary>
    /// <param name="args">Аргументы события использования награды за баллы канала</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    Task<TwitchUser> EnsureUserExistsAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Гарантирует наличие пользователя в БД по TwitchId.
    /// Если пользователя нет - пытается получить данные из API.
    /// </summary>
    /// <param name="twitchId">ID пользователя Twitch</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    Task<TwitchUser> EnsureUserExistsAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Гарантирует наличие пользователя в БД из готовой сущности TwitchUser.
    /// Если пользователь уже существует - обновляет его данные, иначе создает нового.
    /// </summary>
    /// <param name="twitchUser">Готовая сущность TwitchUser</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>TwitchUser из БД</returns>
    Task<TwitchUser> EnsureUserExistsAsync(
        TwitchUser? twitchUser,
        CancellationToken cancellationToken = default
    );
}
