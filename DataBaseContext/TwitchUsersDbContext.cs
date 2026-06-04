using MARS.Server.Services.CinemaQueue.Entitys;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using MARS.Server.Services.Twitch.Rewards.FumoFriday.Entitys;
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
    public DbSet<Host> Hosts { get; set; } = null!;
    public DbSet<FollowerInfo> FollowersEntitys { get; set; } = null!;
    public DbSet<TwitchLeaderboardUser> TwitchLeaderboardUsers { get; set; } = null!;
    public DbSet<HelloVideosUsers> HelloVideosUsers { get; set; } = null!;
    public DbSet<FumoUser> FumoUsers { get; set; } = null!;
    public DbSet<WaifuRollGuarantee> WaifuRollGuarantees { get; set; } = null!;
    public DbSet<HostCoolDown> HostsCoolDowns { get; set; } = null!;
    public DbSet<HostAutoHello> HostsGreetings { get; set; } = null!;

    // Таблицы с опциональными связями к TwitchUser
    public DbSet<QueueItem> SoundRequestQueueItems { get; set; } = null!;
    public DbSet<CinemaMediaItem> CinemaQueue { get; set; } = null!;

    /// <summary>
    /// Конфигурация моделей для таблиц, связанных с TwitchUser
    /// </summary>
    public partial void OnModelCreatingTwitchUsersPartial(ModelBuilder modelBuilder)
    {
        // Конфигурация Host
        modelBuilder
            .Entity<Host>()
            .UseTpcMappingStrategy()
            .HasOne(h => h.HostGreetings)
            .WithOne(hg => hg.Host)
            .HasForeignKey<HostAutoHello>(e => e.HostId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder
            .Entity<Host>()
            .UseTpcMappingStrategy()
            .HasOne(h => h.HostCoolDown)
            .WithOne(hcd => hcd.Host)
            .HasForeignKey<HostCoolDown>(e => e.HostId)
            .OnDelete(DeleteBehavior.NoAction);

        // Связь Host -> TwitchUser (многие к одному)
        modelBuilder
            .Entity<Host>()
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
