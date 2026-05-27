using MARS.Server.ApplicationState;
using MARS.Server.Services._365Genius.Entitys;
using MARS.Server.Services.EnvironmentVariable.Entitys;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.Framedata.Entitys.Pending;
using MARS.Server.Services.Scoreboard.Entitys;
using MARS.Server.Services.ServiceManager.Entitys;
using MARS.Server.Services.SoundRequest.Entities;
using MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;
using MARS.Server.Services.Telegram.DiscordBridge.Entities;
using MARS.Server.Services.Telegram.PrivateChannelsResender.Entities;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;
using MARS.Server.Services.Twitch.Rewards.ChannelRewards.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MARS.Server.DataBaseContext;

public sealed partial class AppDbContext : DbContext
{
    private static readonly Lock Locker = new();
    private static bool _isMigrated;

    public AppDbContext(DbContextOptions<AppDbContext> options, bool isMigrations)
        : base(options)
    {
        if (!_isMigrated && !isMigrations)
        {
            Locker.Enter();
            try
            {
                if (!_isMigrated)
                {
                    Database.Migrate();
                    _isMigrated = true;
                }
            }
            finally
            {
                Locker.Exit();
            }
        }
    }

    // Таблицы Twitch Users вынесены в TwitchUsersDbContext.cs

    public DbSet<Waifu> Waifus { get; set; } = null!;
    public DbSet<TelegramUser> TelegramUsers { get; set; } = null!;
    public DbSet<MediaInfo> Alerts { get; set; } = null!;
    public DbSet<AutoMessage> AutoMessages { get; set; } = null!;
    public DbSet<TokenInfo> TwitchToken { get; set; } = null!;
    public DbSet<MemeOrder> RandomMemeOrder { get; set; } = null!;
    public DbSet<MemeType> RandomMemeType { get; set; } = null!;
    public DbSet<Video365> Videos365 { get; set; } = null!;
    public DbSet<TekkenCharacter> TekkenCharacters { get; set; } = null!;
    public DbSet<Move> TekkenMoves { get; set; } = null!;
    public DbSet<TekkenCharacterPending> TekkenCharactersPending { get; set; } = null!;
    public DbSet<MovePending> TekkenMovesPending { get; set; } = null!;
    public DbSet<TelegramUpdateReceiverOffset> TelegramUpdateReceiverOffset { get; set; } = null!;
    public DbSet<WTelegramAlloweedChannel> WTelegramAlloweedChannels { get; set; } = null!;
    public DbSet<RootState> RootState { get; set; } = null!;

    // SoundRequest - новая структура
    public DbSet<BaseTrackInfo> SoundRequestBaseTrackInfos { get; set; } = null!;
    public DbSet<PlayerState> SoundRequestPlayerState { get; set; } = null!;

    public DbSet<ServiceState> ServiceStates { get; set; } = null!;
    public DbSet<ScoreboardState> ScoreboardStates { get; set; } = null!;
    public DbSet<ScoreboardPlayer> ScoreboardPlayers { get; set; } = null!;
    public DbSet<ScoreboardLayout> ScoreboardLayouts { get; set; } = null!;
    public DbSet<StreamArchiveConfig> StreamArchiveConfigs { get; set; } = null!;
    public DbSet<StreamArchiveFile> StreamArchiveFiles { get; set; } = null!;
    public DbSet<StreamArchiveFileChunk> StreamArchiveFileChunks { get; set; } = null!;
    public DbSet<ChannelRewardRecord> ChannelRewards { get; set; } = null!;
    public DbSet<MikuMondayTrack> MikuMondayTracks { get; set; } = null!;
    public DbSet<MikuMondayActivation> MikuMondayActivations { get; set; } = null!;
    public DbSet<EnvironmentVariable> EnvironmentVariables { get; set; } = null!;
    public DbSet<ChannelProcessingState> ChannelProcessingStates { get; set; } = null!;
    public DbSet<TelegramDiscordChannelBinding> TelegramDiscordChannelBindings { get; set; } =
        null!;
    public DbSet<TelegramDiscordChannelState> TelegramDiscordChannelStates { get; set; } = null!;

    /// <summary>
    /// Partial метод для конфигурации таблиц, связанных с TwitchUser (реализован в TwitchUsersDbContext.cs)
    /// </summary>
    public partial void OnModelCreatingTwitchUsersPartial(ModelBuilder modelBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Конфигурации для таблиц, связанных с TwitchUser
        OnModelCreatingTwitchUsersPartial(modelBuilder);

        // Конфигурация HelloVideosUsers -> MediaInfo (остальное в TwitchUsersDbContext)
        modelBuilder
            .Entity<HelloVideosUsers>()
            .HasOne(e => e.MediaInfo)
            .WithOne()
            .HasForeignKey<HelloVideosUsers>(e => e.MediaInfoId);

        modelBuilder.Entity<MemeOrder>().HasKey(e => e.Id);
        modelBuilder
            .Entity<MemeOrder>()
            .HasOne(mo => mo.Type)
            .WithMany()
            .HasForeignKey(mo => mo.MemeTypeId);

        modelBuilder
            .Entity<MemeType>()
            .HasData(
                [
                    new MemeType
                    {
                        Name = "Random Sound",
                        Id = 3,
                        FolderPath = "Alerts\\zvik",
                    },
                    new MemeType
                    {
                        Name = "Random Meme",
                        Id = 2,
                        FolderPath = "Alerts\\random_meme",
                    },
                ]
            );

        // Конфигурация для SoundRequest - новая структура

        // BaseTrackInfo: уникальный индекс на URL
        modelBuilder.Entity<BaseTrackInfo>().HasIndex(t => t.Url).IsUnique();

        // QueueItem: индекс на QueueOrder (связь с TwitchUser в TwitchUsersDbContext)
        modelBuilder.Entity<QueueItem>().HasIndex(qi => qi.QueueOrder);

        // PlayerState: связь с текущим QueueItem
        modelBuilder
            .Entity<PlayerState>()
            .HasOne(ps => ps.CurrentQueueItem)
            .WithMany()
            .HasForeignKey(ps => ps.CurrentQueueItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MediaInfo>(entity =>
        {
            entity.OwnsOne(
                e => e.TextInfo,
                textInfo =>
                {
                    textInfo.Property(p => p.Text).HasColumnName("TextInfo_Text");
                    textInfo.Property(p => p.TextColor).HasColumnName("TextInfo_TextColor");
                    textInfo.Property(p => p.TriggerWord).HasColumnName("TextInfo_TriggerWord");
                    textInfo.Property(p => p.KeyWordsColor).HasColumnName("TextInfo_KeyWordsColor");
                }
            );

            entity.OwnsOne(
                e => e.FileInfo,
                fileInfo =>
                {
                    fileInfo.Property(p => p.FileName).HasColumnName("FileInfo_FileName");
                    fileInfo.Property(p => p.FilePath).HasColumnName("FileInfo_LocalFilePath");
                    fileInfo.Property(p => p.Extension).HasColumnName("FileInfo_Extension");
                    fileInfo.Property(p => p.IsLocalFile).HasColumnName("FileInfo_IsLocal");
                    fileInfo
                        .Property(p => p.Type)
                        .HasColumnName("FileInfo_Type")
                        .HasConversion<string>();
                }
            );

            entity.OwnsOne(
                e => e.PositionInfo,
                positionInfo =>
                {
                    positionInfo.Property(p => p.Height).HasColumnName("PositionInfo_Height");
                    positionInfo.Property(p => p.Width).HasColumnName("PositionInfo_Width");
                    positionInfo.Property(p => p.Rotation).HasColumnName("PositionInfo_Rotation");
                    positionInfo
                        .Property(p => p.RandomCoordinates)
                        .HasColumnName("PositionInfo_RandomCoordinates");
                    positionInfo
                        .Property(p => p.IsProportion)
                        .HasColumnName("PositionInfo_IsProportion");
                    positionInfo.Property(p => p.IsRotated).HasColumnName("PositionInfo_IsRotated");
                    positionInfo
                        .Property(p => p.XCoordinate)
                        .HasColumnName("PositionInfo_XCoordinate");
                    positionInfo
                        .Property(p => p.YCoordinate)
                        .HasColumnName("PositionInfo_YCoordinate");
                    positionInfo
                        .Property(p => p.IsResizeRequires)
                        .HasColumnName("PositionInfo_IsResizeRequires");
                    positionInfo
                        .Property(p => p.IsHorizontalCenter)
                        .HasColumnName("PositionInfo_IsHorizontalCenter");
                    positionInfo
                        .Property(p => p.IsVerticallCenter)
                        .HasColumnName("PositionInfo_IsVerticallCenter");
                }
            );

            entity.OwnsOne(
                e => e.MetaInfo,
                metaInfo =>
                {
                    metaInfo.Property(p => p.DisplayName).HasColumnName("MetaInfo_DisplayName");
                    metaInfo.Property(p => p.IsLooped).HasColumnName("MetaInfo_IsLooped");
                    metaInfo.Property(p => p.Duration).HasColumnName("MetaInfo_Duration");
                    metaInfo
                        .Property(p => p.TwitchPointsCost)
                        .HasColumnName("MetaInfo_TwitchPointsCost");
                    metaInfo.Property(p => p.Vip).HasColumnName("MetaInfo_VIP");
                    metaInfo.Property(e => e.Priority).HasColumnName("MetaInfo_Priority");
                }
            );

            entity.OwnsOne(
                e => e.StylesInfo,
                metaInfo =>
                {
                    metaInfo.Property(p => p.IsBorder).HasColumnName("StylesInfo_IsBorder");
                    metaInfo
                        .Property(p => p.IsShowLetterbox)
                        .HasColumnName("StylesInfo_IsShowLetterbox");
                }
            );
        });

        // No owned types for BaseTrackInfo in new SoundRequest

        modelBuilder.Entity<Move>().HasKey(o => new { o.CharacterName, o.Command });
        modelBuilder.Entity<MovePending>().ToTable("TekkenMovesPending");
        modelBuilder.Entity<MovePending>().HasKey(o => new { o.CharacterName, o.Command });

        modelBuilder
            .Entity<Move>()
            .HasOne(m => m.Character)
            .WithMany(c => c.Movelist)
            .HasForeignKey(e => e.CharacterName)
            .OnDelete(DeleteBehavior.NoAction); // assuming you add a CharacterId property to Move

        modelBuilder
            .Entity<TekkenCharacter>()
            .HasMany(c => c.Movelist)
            .WithOne(m => m.Character)
            .HasForeignKey(e => e.CharacterName)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<TekkenCharacterPending>().ToTable("TekkenCharactersPending");

        modelBuilder
            .Entity<TekkenCharacter>()
            .Property(e => e.Image)
            .HasColumnType("varbinary(max)");

        modelBuilder.Entity<RootState>().ToTable("RootState");
        modelBuilder.Entity<RootState>().HasIndex(e => e.Name).IsUnique();
        modelBuilder
            .Entity<RootState>()
            .HasData(
                [
                    new RootState
                    {
                        Name = RootStateKeys.RandomMemeOnlineIsStop,
                        Value = false.ToString(),
                        Description = "Флаг остановки сервиса RandomMemeOnline",
                        TypeDescription = "bool",
                    },
                    new RootState
                    {
                        Name = RootStateKeys.PuntoSwitcherFilterEnabled,
                        Value = true.ToString(),
                        Description = "Флаг включения фильтра PuntoSwitcher",
                        TypeDescription = "bool",
                    },
                    new RootState
                    {
                        Name = RootStateKeys.WaifuRollCooldownMinutes,
                        Value = 20L.ToString(),
                        Description = "Кулдаун ролла вайфу в минутах",
                        TypeDescription = "long",
                    },
                    new RootState
                    {
                        Name = RootStateKeys.WTelegramMtProxyUrl,
                        Value = string.Empty,
                        Description =
                            "MTProxy URL для WTelegram (например: https://t.me/proxy?server=...)",
                        TypeDescription = "string",
                    },
                    new RootState
                    {
                        Name = RootStateKeys.WTelegramProxyUrl,
                        Value = string.Empty,
                        Description =
                            "Прокси для WTelegram: socks5://user:pass@host:port или http://user:pass@host:port",
                        TypeDescription = "string",
                    },
                ]
            );

        // Конфигурация для Scoreboard
        modelBuilder
            .Entity<ScoreboardState>()
            .HasMany(s => s.Players)
            .WithOne(p => p.ScoreboardState)
            .HasForeignKey(p => p.ScoreboardStateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<ScoreboardPlayer>()
            .HasIndex(p => new { p.ScoreboardStateId, p.Position })
            .IsUnique();

        modelBuilder
            .Entity<ScoreboardLayout>()
            .HasOne(l => l.ScoreboardState)
            .WithOne(s => s.Layout)
            .HasForeignKey<ScoreboardLayout>(l => l.ScoreboardStateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Конфигурация для StreamArchive
        modelBuilder
            .Entity<StreamArchiveFile>()
            .HasOne(f => f.Config)
            .WithMany()
            .HasForeignKey(f => f.ConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<StreamArchiveFileChunk>()
            .HasOne(c => c.File)
            .WithMany(f => f.Chunks)
            .HasForeignKey(c => c.FileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<StreamArchiveFile>()
            .HasIndex(f => new { f.ConfigId, f.OriginalFilePath })
            .IsUnique();

        // Конфигурация для MikuMonday
        modelBuilder.Entity<MikuMondayTrack>().HasIndex(t => t.Number).IsUnique();

        modelBuilder
            .Entity<MikuMondayTrack>()
            .HasOne(mt => mt.BaseTrackInfo)
            .WithMany()
            .HasForeignKey(mt => mt.BaseTrackInfoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder
            .Entity<MikuMondayActivation>()
            .HasOne(a => a.MikuMondayTrack)
            .WithMany()
            .HasForeignKey(a => a.MikuMondayTrackId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder
            .Entity<MikuMondayActivation>()
            .HasIndex(a => new
            {
                a.TwitchUserId,
                a.Year,
                a.WeekOfYear,
            })
            .IsUnique();

        modelBuilder
            .Entity<TelegramDiscordChannelBinding>()
            .HasIndex(e => new { e.TelegramChannelId, e.DiscordChannelId })
            .IsUnique();

        modelBuilder
            .Entity<TelegramDiscordChannelBinding>()
            .Property(e => e.DiscordChannelId)
            .HasConversion(new NumberToStringConverter<ulong>());

        modelBuilder.Entity<TelegramDiscordChannelState>().HasKey(e => e.TelegramChannelId);

        // Конфигурация для EnvironmentVariable
        modelBuilder.Entity<EnvironmentVariable>().HasIndex(e => e.Key).IsUnique();

        // Конфигурация связей с TwitchUser вынесена в TwitchUsersDbContext

        // Старая конфигурация PlayerState закомментирована - используется новая выше
        // modelBuilder
        //     .Entity<PlayerState>()
        //     .HasOne(ps => ps.CurrentTrack)
        //     .WithOne()
        //     .HasForeignKey<PlayerState>(ps => ps.CurrentTrackId)
        //     .OnDelete(DeleteBehavior.Restrict);

        // modelBuilder
        //     .Entity<PlayerState>()
        //     .HasOne(ps => ps.NextTrack)
        //     .WithOne()
        //     .HasForeignKey<PlayerState>(ps => ps.NextTrackId)
        //     .OnDelete(DeleteBehavior.Restrict);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConversion>();

        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeToDateTimeUtc>();
    }

    public sealed class DateTimeOffsetConversion()
        : ValueConverter<DateTimeOffset, DateTimeOffset>(
            offset => offset.Offset != TimeSpan.Zero ? offset.ToOffset(TimeSpan.Zero) : offset,
            v => v.ToLocalTime()
        );

    public sealed class DateTimeToDateTimeUtc()
        : ValueConverter<DateTime, DateTime>(
            c => DateTime.SpecifyKind(c, DateTimeKind.Utc),
            c => c.ToLocalTime().AddHours(-4)
        );
}
