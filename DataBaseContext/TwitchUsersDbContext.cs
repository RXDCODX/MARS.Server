using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using MARS.Server.Services.Twitch.Rewards._13_FumoFriday.Entitys;
using MARS.Server.Services.Twitch.TwitchFollowers.Entitys;
using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.EntityFrameworkCore;

namespace MARS.Server.DataBaseContext;

/// <summary>
/// Контекст базы данных для таблиц, связанных с пользователями Twitch
/// </summary>
public sealed partial class AppDbContext
{
    // Основная таблица пользователей Twitch
    public DbSet<TwitchUser> TwitchUsers { get; set; } = null!;

    // Таблицы, связанные с пользователями Twitch
    public DbSet<Husband> Husbands { get; set; } = null!;
    public DbSet<FollowerInfo> FollowersEntitys { get; set; } = null!;
    public DbSet<TwitchLeaderboardUser> TwitchLeaderboardUsers { get; set; } = null!;
    public DbSet<HelloVideosUsers> HelloVideosUsers { get; set; } = null!;
    public DbSet<FumoUser> FumoUsers { get; set; } = null!;
    public DbSet<WaifuRollGuarantee> WaifuRollGuarantees { get; set; } = null!;
    public DbSet<HusbandCoolDown> HusbandCoolDowns { get; set; } = null!;
    public DbSet<HusbandAutoHello> HusbandGreetings { get; set; } = null!;

    // Таблицы с опциональными связями к TwitchUser
    public DbSet<QueueItem> SoundRequestQueueItems { get; set; } = null!;
    public DbSet<CinemaMediaItem> CinemaQueue { get; set; } = null!;

    /// <summary>
    /// Конфигурация моделей для таблиц, связанных с TwitchUser
    /// </summary>
    public partial void OnModelCreatingTwitchUsersPartial(ModelBuilder modelBuilder)
    {
        // Конфигурация Husband
        modelBuilder
            .Entity<Husband>()
            .UseTpcMappingStrategy()
            .HasOne(h => h.HusbandGreetings)
            .WithOne(hg => hg.Husband)
            .HasForeignKey<HusbandAutoHello>(e => e.HusbandId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder
            .Entity<Husband>()
            .UseTpcMappingStrategy()
            .HasOne(h => h.HusbandCoolDown)
            .WithOne(hcd => hcd.Husband)
            .HasForeignKey<HusbandCoolDown>(e => e.HusbandId)
            .OnDelete(DeleteBehavior.NoAction);

        // Связь Husband -> TwitchUser (многие к одному)
        modelBuilder
            .Entity<Husband>()
            .HasOne(h => h.TwitchUser)
            .WithMany()
            .HasForeignKey(h => h.TwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация FollowerInfo -> TwitchUser (один к одному)
        modelBuilder
            .Entity<FollowerInfo>()
            .HasOne(fi => fi.TwitchUser)
            .WithOne()
            .HasForeignKey<FollowerInfo>(fi => fi.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация TwitchLeaderboardUser -> TwitchUser
        modelBuilder
            .Entity<TwitchLeaderboardUser>()
            .HasOne(tlu => tlu.TwitchUser)
            .WithMany()
            .HasForeignKey(tlu => tlu.TwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация HelloVideosUsers -> TwitchUser
        modelBuilder
            .Entity<HelloVideosUsers>()
            .HasOne(hvu => hvu.TwitchUser)
            .WithMany()
            .HasForeignKey(hvu => hvu.TwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация FumoUser -> TwitchUser
        modelBuilder
            .Entity<FumoUser>()
            .HasOne(fu => fu.TwitchUser)
            .WithMany()
            .HasForeignKey(fu => fu.TwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация WaifuRollGuarantee -> TwitchUser
        modelBuilder
            .Entity<WaifuRollGuarantee>()
            .HasOne(wrg => wrg.TwitchUser)
            .WithMany()
            .HasForeignKey(wrg => wrg.TwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация QueueItem -> TwitchUser
        modelBuilder
            .Entity<QueueItem>()
            .HasOne(qi => qi.RequestedByTwitchUser)
            .WithMany()
            .HasForeignKey(qi => qi.RequestedByTwitchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Конфигурация CinemaMediaItem -> TwitchUser (опциональная связь)
        modelBuilder
            .Entity<CinemaMediaItem>()
            .HasOne(cmi => cmi.TwitchUser)
            .WithMany()
            .HasForeignKey(cmi => cmi.TwitchUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
