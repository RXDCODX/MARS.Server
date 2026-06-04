using System.Diagnostics;
using TwitchLib.Api.Helix.Models.Channels.GetChannelVIPs;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using ChatMessage = TwitchLib.Client.Models.ChatMessage;
using User = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace MARS.Server.Services.Twitch.Entitys;

/// <summary>
/// Основная информация о пользователе Twitch
/// </summary>
[Table("TwitchUsers")]
[DebuggerDisplay("{DisplayName} ({UserLogin}) - ID: {TwitchId}")]
public class TwitchUser
{
    /// <summary>
    /// ID пользователя Twitch (primary key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(50)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column(nameof(TwitchId))]
    public required string TwitchId
    {
        get;
        init
        {
            if (!IsValidTwitchId(value))
            {
                throw new ArgumentException("TwitchId was not valid");
            }
            field = value;
        }
    }

    /// <summary>
    /// Логин пользователя
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string UserLogin { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Ссылка на аватарку пользователя
    /// </summary>
    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// Цвет ника пользователя в чате
    /// </summary>
    [MaxLength(20)]
    public string? ChatColor { get; set; }

    /// <summary>
    /// Является ли пользователь модератором
    /// </summary>
    public bool IsModerator { get; set; }

    /// <summary>
    /// Является ли пользователь VIP
    /// </summary>
    public bool IsVip { get; set; }

    /// <summary>
    /// Является ли пользователь стримером (broadcaster)
    /// </summary>
    public bool IsBroadcaster => TwitchId == TwitchExstension.ChannelId;

    /// <summary>
    /// Дата подписки на канал
    /// </summary>
    public DateTime? FollowedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления информации
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Флаг указывающий, что юзер в черном списке
    /// </summary>
    public bool IsInBlockList { get; set; } = false;

    /// <summary>
    /// Проверить, является ли пользователь просто фоловером
    /// </summary>
    [NotMapped]
    public bool IsSimpleUser => !IsModerator && !IsVip && !IsBroadcaster;

    public override string ToString()
    {
        return $"{DisplayName} ({UserLogin}) - ID: {TwitchId}";
    }

    public override bool Equals(object? obj)
    {
        return obj is TwitchUser other && TwitchId == other.TwitchId;
    }

    public override int GetHashCode()
    {
        return TwitchId.GetHashCode();
    }

    /// <summary>
    /// Проверяет, является ли TwitchId валидным (должен быть числовым)
    /// </summary>
    private static bool IsValidTwitchId(string twitchId)
    {
        // TwitchId должен быть числовым (не GUID или другая строка)
        return !string.IsNullOrWhiteSpace(twitchId) && long.TryParse(twitchId, out _);
    }

    #region Static Factory Methods

    /// <summary>
    /// Создает объект TwitchUser из ChatMessage
    /// </summary>
    /// <param name="chatMessage">Сообщение из чата</param>
    /// <returns>Объект TwitchUser или null, если данные невалидны</returns>
    public static TwitchUser? FromChatMessage(ChatMessage? chatMessage)
    {
        TwitchUser? result = null;

        if (chatMessage != null && !string.IsNullOrWhiteSpace(chatMessage.UserId))
        {
            if (IsValidTwitchId(chatMessage.UserId))
            {
                result = new TwitchUser
                {
                    TwitchId = chatMessage.UserId,
                    UserLogin = chatMessage.Username,
                    DisplayName = chatMessage.DisplayName,
                    ChatColor = chatMessage.HexColor,
                    IsModerator = chatMessage.UserDetail.IsModerator,
                    IsVip = chatMessage.UserDetail.IsVip,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Создает объект TwitchUser из OnMessageReceivedArgs
    /// </summary>
    /// <param name="args">Аргументы события получения сообщения</param>
    /// <returns>Объект TwitchUser или null, если данные невалидны</returns>
    public static TwitchUser? FromOnMessageReceivedArgs(OnMessageReceivedArgs? args)
    {
        return args?.ChatMessage != null ? FromChatMessage(args.ChatMessage) : null;
    }

    /// <summary>
    /// Создает объект TwitchUser из ChannelPointsCustomRewardRedemptionArgs
    /// </summary>
    /// <param name="args">Аргументы события использования награды за баллы канала</param>
    /// <returns>Объект TwitchUser или null, если данные невалидны</returns>
    public static TwitchUser? FromChannelPointsCustomRewardRedemptionArgs(
        ChannelPointsCustomRewardRedemptionArgs? args
    )
    {
        TwitchUser? result = null;

        if (args?.Payload?.Event != null)
        {
            var evt = args.Payload.Event;
            if (!string.IsNullOrWhiteSpace(evt.UserId) && IsValidTwitchId(evt.UserId))
            {
                result = new TwitchUser
                {
                    TwitchId = evt.UserId,
                    UserLogin = evt.UserLogin,
                    DisplayName = evt.UserName,
                    IsModerator = false,
                    IsVip = false,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Создает объект TwitchUser по минимальным данным (только ID и опционально логин/имя)
    /// </summary>
    /// <param name="user"></param>
    /// <returns>Объект TwitchUser или null, если TwitchId невалиден</returns>
    public static TwitchUser? FromUser(User user)
    {
        return new TwitchUser
        {
            TwitchId = user.Id,
            UserLogin = user.Login ?? $"user_{user.Id}",
            DisplayName = user.DisplayName ?? user.Login ?? $"User{user.Id}",
            ProfileImageUrl = user.ProfileImageUrl,
            IsModerator = false,
            IsVip = false,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
        };
    }

    public static TwitchUser FromModerator(Moderator mod)
    {
        return new TwitchUser
        {
            TwitchId = mod.UserId,
            UserLogin = mod.UserLogin,
            DisplayName = mod.UserName,
            CreatedAt = DateTime.Now,
            LastUpdated = DateTime.Now,
            IsModerator = true,
        };
    }

    public static TwitchUser? FromVip(ChannelVIPsResponseModel vip)
    {
        return new TwitchUser
        {
            DisplayName = vip.UserName,
            TwitchId = vip.UserId,
            UserLogin = vip.UserLogin,
            LastUpdated = DateTime.Now,
            CreatedAt = DateTime.Now,
            IsVip = true,
        };
    }

    public static TwitchUser? FromApiUser(User apiuser)
    {
        return new TwitchUser()
        {
            DisplayName = apiuser.DisplayName,
            TwitchId = apiuser.Id,
            UserLogin = apiuser.Login,
            CreatedAt = apiuser.CreatedAt,
            ProfileImageUrl = apiuser.ProfileImageUrl,
        };
    }

    #endregion
}
