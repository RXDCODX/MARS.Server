namespace MARS.Server.Services.Twitch.TwitchFollowers.Entitys;

/// <summary>
/// Entity для хранения информации о фоловерах в базе данных
/// </summary>
public class FollowerDbEntity
{
    /// <summary>
    /// ID пользователя (первичный ключ)
    /// </summary>
    [Key]
    [Required]
    public required string UserId { get; set; }

    /// <summary>
    /// Логин пользователя
    /// </summary>
    [Required]
    public required string UserLogin { get; set; }

    /// <summary>
    /// Имя пользователя
    /// </summary>
    [Required]
    public required string UserName { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Ссылка на аватарку пользователя
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// Цвет ника пользователя в чате
    /// </summary>
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
    /// Дата подписки на канал
    /// </summary>
    public DateTime FollowedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления информации
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата создания записи в БД
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 

    /// <summary>
    /// Преобразовать в FollowerInfo
    /// </summary>
    /// <returns>Объект FollowerInfo</returns>
    public FollowerInfo ToFollowerInfo()
    {
        return new FollowerInfo
        {
            UserId = UserId,
            UserLogin = UserLogin,
            UserName = UserName,
            DisplayName = DisplayName,
            ProfileImageUrl = ProfileImageUrl,
            ChatColor = ChatColor,
            IsModerator = IsModerator,
            IsVip = IsVip,
            FollowedAt = FollowedAt,
            LastUpdated = LastUpdated,
        };
    }

    /// <summary>
    /// Создать FollowerDbEntity из FollowerInfo
    /// </summary>
    /// <param name="followerInfo">Информация о фоловере</param>
    /// <returns>Новый экземпляр FollowerDbEntity</returns>
    public static FollowerDbEntity FromFollowerInfo(FollowerInfo followerInfo)
    {
        return new FollowerDbEntity
        {
            UserId = followerInfo.UserId,
            UserLogin = followerInfo.UserLogin,
            UserName = followerInfo.UserName,
            DisplayName = followerInfo.DisplayName,
            ProfileImageUrl = followerInfo.ProfileImageUrl,
            ChatColor = followerInfo.ChatColor,
            IsModerator = followerInfo.IsModerator,
            IsVip = followerInfo.IsVip,
            FollowedAt = followerInfo.FollowedAt,
            LastUpdated = followerInfo.LastUpdated,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Обновить информацию из FollowerInfo
    /// </summary>
    /// <param name="followerInfo">Новая информация о фоловере</param>
    public void UpdateFromFollowerInfo(FollowerInfo followerInfo)
    {
        UserLogin = followerInfo.UserLogin;
        UserName = followerInfo.UserName;
        DisplayName = followerInfo.DisplayName;
        ProfileImageUrl = followerInfo.ProfileImageUrl;
        ChatColor = followerInfo.ChatColor;
        IsModerator = followerInfo.IsModerator;
        IsVip = followerInfo.IsVip;
        FollowedAt = followerInfo.FollowedAt;
        LastUpdated = followerInfo.LastUpdated;
    }
}
