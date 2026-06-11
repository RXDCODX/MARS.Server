using MARS.Server.Services._365Genius.Entitys;
using MARS.Server.Services.EnvironmentVariable.Entitys;
using MARS.Server.Services.Framedata.Entitys.Pending;
using MARS.Server.Services.Scoreboard.Entitys;
using MARS.Server.Services.ServiceManager.Entitys;
using MARS.Server.Services.StreamAcrhive_UNUSED.Entitys;
using MARS.Server.Services.Telegram.DiscordBridge.Entities;
using MARS.Server.Services.Telegram.PrivateChannelsResender.Entities;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
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
    public DbSet<Fumo> Fumos { get; set; } = null!;
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
            .HasData([
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
            ]);

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
                        .Property(p => p.IsFileNotConvertable)
                        .HasColumnName("FileInfo_IsFileNotConvertable");

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
            .HasData([
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
            ]);

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

        // Seed Fumo data
        modelBuilder
            .Entity<Fumo>()
            .HasData([
                new Fumo
                {
                    MfcId = 73436,
                    Name =
                        "Touhou Project - Huziwara no Mokou - FumoFumo - Touhou Plush Series  (18) (Gift)",
                    Character = "Huziwara no Mokou",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/73436.jpg",
                    Rating = 9.52,
                    RatingCount = 60,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 73437,
                    Name =
                        "Touhou Project - Houraisan Kaguya - FumoFumo - Touhou Plush Series  (17) (Angeltype, Gift)",
                    Character = "Houraisan Kaguya",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/73437.jpg",
                    Rating = 9.63,
                    RatingCount = 57,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 96113,
                    Name =
                        "Touhou Project - Komeiji Koishi - FumoFumo - Touhou Plush Series  (20) (Gift)",
                    Character = "Komeiji Koishi",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/96113.jpg",
                    Rating = 9.43,
                    RatingCount = 96,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 96114,
                    Name =
                        "Touhou Project - Komeiji Satori - FumoFumo - Touhou Plush Series  (19) (Gift)",
                    Character = "Komeiji Satori",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/96114.jpg",
                    Rating = 9.7,
                    RatingCount = 60,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 139845,
                    Name =
                        "Touhou Project - Inaba Tewi - FumoFumo - Touhou Plush Series  (22) (Gift)",
                    Character = "Inaba Tewi",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/139845.jpg",
                    Rating = 9.8,
                    RatingCount = 65,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 154203,
                    Name =
                        "Touhou Project - Reisen Udongein Inaba - FumoFumo - Touhou Plush Series  (21) (Gift)",
                    Character = "Reisen Udongein Inaba",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/154203.jpg",
                    Rating = 9.66,
                    RatingCount = 80,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 236827,
                    Name =
                        "Touhou Project - Kotiya Sanae - FumoFumo - Touhou Plush Series  (24) - ver.2 (Gift)",
                    Character = "Kotiya Sanae",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/236827.jpg",
                    Rating = 9.57,
                    RatingCount = 44,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 265949,
                    Name =
                        "Touhou Project - Hata no Kokoro - FumoFumo - Touhou Plush Series  (#25) (Gift)",
                    Character = "Hata no Kokoro",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/265949.jpg",
                    Rating = 9.86,
                    RatingCount = 69,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 303910,
                    Name =
                        "Touhou Project - Hakurei Reimu - FumoFumo - Touhou Plush Series  (27) - Kourindou ver. (Gift)",
                    Character = "Hakurei Reimu",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/303910.jpg",
                    Rating = 9.41,
                    RatingCount = 32,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 307048,
                    Name =
                        "Touhou Project - Flandre Scarlet - FumoFumo - Touhou Plush Series  (26) - ver. 1.5 (Gift)",
                    Character = "Flandre Scarlet",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/307048.jpg",
                    Rating = 9.54,
                    RatingCount = 67,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 329357,
                    Name =
                        "Touhou Project - Remilia Scarlet - FumoFumo - Touhou Plush Series  (28) - Kourindou ver. (Gift)",
                    Character = "Remilia Scarlet",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/329357.jpg",
                    Rating = 9.55,
                    RatingCount = 40,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 424435,
                    Name =
                        "Touhou Project - Kirisame Marisa - FumoFumo - Touhou Plush Series  (31) - Kourindou ver. (Gift)",
                    Character = "Kirisame Marisa",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/424435.jpg",
                    Rating = 9.35,
                    RatingCount = 23,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 561800,
                    Name =
                        "Touhou Project - Himekaidou Hatate - FumoFumo - Touhou Plush Series  (34) (Gift)",
                    Character = "Himekaidou Hatate",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/561800.jpg",
                    Rating = 9.07,
                    RatingCount = 29,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 562445,
                    Name =
                        "Touhou Project - Alice Margatroid - FumoFumo - Touhou Plush Series  (35) - ver. 1.5 (Gift)",
                    Character = "Alice Margatroid",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/562445.jpg",
                    Rating = 9.59,
                    RatingCount = 46,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 562451,
                    Name =
                        "Touhou Project - Patchouli Knowledge - FumoFumo - Touhou Plush Series  (36) - ver. 1.5 (Gift)",
                    Character = "Patchouli Knowledge",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/562451.jpg",
                    Rating = 9.68,
                    RatingCount = 84,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 562452,
                    Name =
                        "Touhou Project - Konpaku Youmu - FumoFumo - Touhou Plush Series  (37) - ver. 1.5 (Gift)",
                    Character = "Konpaku Youmu",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/562452.jpg",
                    Rating = 9.79,
                    RatingCount = 47,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 562456,
                    Name =
                        "Touhou Project - Saigyouzi Yuyuko - FumoFumo - Touhou Plush Series  (38) - ver. 1.5 (Gift)",
                    Character = "Saigyouzi Yuyuko",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/562456.jpg",
                    Rating = 9.64,
                    RatingCount = 59,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 630801,
                    Name =
                        "Touhou Project - Yakumo Ran - FumoFumo - Touhou Plush Series  (40) - ver.1.5 (Gift)",
                    Character = "Yakumo Ran",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/630801.jpg",
                    Rating = 9.74,
                    RatingCount = 38,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 630802,
                    Name =
                        "Touhou Project - Chen - FumoFumo - Touhou Plush Series  (39) - ver.1.5 (Gift)",
                    Character = "Chen",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/630802.jpg",
                    Rating = 9.79,
                    RatingCount = 28,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 630803,
                    Name =
                        "Touhou Project - Yakumo Yukari - FumoFumo - Touhou Plush Series  (41) - ver.1.5 (Gift)",
                    Character = "Yakumo Yukari",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/630803.jpg",
                    Rating = 9.84,
                    RatingCount = 32,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 762082,
                    Name =
                        "Touhou Project - Hinanai Tenshi - FumoFumo - Touhou Plush Series  (44) (Gift)",
                    Character = "Hinanai Tenshi",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/762082.jpg",
                    Rating = 9.84,
                    RatingCount = 51,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 822816,
                    Name =
                        "Touhou Project - Yorigami Shion - FumoFumo - Touhou Plush Series  (45) (Gift)",
                    Character = "Yorigami Shion",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/822816.jpg",
                    Rating = 9.76,
                    RatingCount = 37,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 852969,
                    Name =
                        "Touhou Project - Cirno - FumoFumo - Touhou Plush Series  (42) - ver.1.5 (Gift)",
                    Character = "Cirno",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/852969.jpg",
                    Rating = 9.84,
                    RatingCount = 80,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 887328,
                    Name =
                        "Touhou Project - Kazami Yuuka - FumoFumo - Touhou Plush Series  (46) (Gift)",
                    Character = "Kazami Yuuka",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/887328.jpg",
                    Rating = 9.65,
                    RatingCount = 40,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 895822,
                    Name =
                        "Touhou Project - Remilia Scarlet - FumoFumo - Touhou Plush Series  (47) - ver.1.5 (Gift)",
                    Character = "Remilia Scarlet",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/895822.jpg",
                    Rating = 9.66,
                    RatingCount = 53,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1055132,
                    Name =
                        "Touhou Project - Inubashiri Momizi - FumoFumo - Touhou Plush Series  (48) (Gift)",
                    Character = "Inubashiri Momizi",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1055132.jpg",
                    Rating = 9.5,
                    RatingCount = 40,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1136463,
                    Name =
                        "Touhou Project - Yagokoro Eirin - FumoFumo - Touhou Plush Series  (49) (Gift)",
                    Character = "Yagokoro Eirin",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1136463.jpg",
                    Rating = 9.7,
                    RatingCount = 27,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1324911,
                    Name =
                        "Touhou Project - Kawashiro Nitori - FumoFumo - Touhou Plush Series  (52) (Gift)",
                    Character = "Kawashiro Nitori",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1324911.jpg",
                    Rating = 9.8,
                    RatingCount = 25,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1324912,
                    Name = "Touhou Project - Rumia - FumoFumo - Touhou Plush Series  (50) (Gift)",
                    Character = "Rumia",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1324912.jpg",
                    Rating = 9.97,
                    RatingCount = 30,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1324913,
                    Name =
                        "Touhou Project - Sikieiki Yamaxanadu - FumoFumo - Touhou Plush Series  (51) (Gift)",
                    Character = "Sikieiki Yamaxanadu",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1324913.jpg",
                    Rating = 9.65,
                    RatingCount = 20,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1324916,
                    Name =
                        "Touhou Project - Yorigami Joon - FumoFumo - Touhou Plush Series  (53) (Gift)",
                    Character = "Yorigami Joon",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1324916.jpg",
                    Rating = 9.62,
                    RatingCount = 29,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1363245,
                    Name =
                        "Touhou Project - Remilia Scarlet - Deka Fumo - Touhou Plush Series  (EX11) - ver.1.5 (Gift)",
                    Character = "Remilia Scarlet",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1363245.jpg",
                    Rating = 9.5,
                    RatingCount = 8,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1518176,
                    Name =
                        "Touhou Project - Toyosatomimi no Miko - FumoFumo - Touhou Plush Series  (57) (Gift)",
                    Character = "Toyosatomimi no Miko",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1518176.jpg",
                    Rating = 9.73,
                    RatingCount = 22,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1518178,
                    Name =
                        "Touhou Project - Hakurei Reimu - FumoFumo - Touhou Plush Series  (54) - ver. 1.5 (Gift)",
                    Character = "Hakurei Reimu",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1518178.jpg",
                    Rating = 9.66,
                    RatingCount = 118,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1518179,
                    Name =
                        "Touhou Project - Kirisame Marisa - FumoFumo - Touhou Plush Series  (55) - ver. 1.5 (Gift)",
                    Character = "Kirisame Marisa",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1518179.jpg",
                    Rating = 9.6,
                    RatingCount = 86,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1549176,
                    Name =
                        "Touhou Lost Word - Konpaku Youmu - FumoFumo - Touhou Plush Series  (63) - Mysterious Master Swordsman ver. (Gift)",
                    Character = "Konpaku Youmu",
                    Origin = "Touhou Lost Word",
                    ThumbnailUrl = "/fumos/1549176.jpg",
                    Rating = 9.51,
                    RatingCount = 51,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1603697,
                    Name =
                        "Touhou Project - Saigyouzi Yuyuko - Deka Fumo - Touhou Plush Series  (EX14) (Gift)",
                    Character = "Saigyouzi Yuyuko",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1603697.jpg",
                    Rating = 9.33,
                    RatingCount = 3,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1661543,
                    Name =
                        "Touhou Project - Hong Meirin - FumoFumo - Touhou Plush Series  (58) - Ver. 1.5 (Gift)",
                    Character = "Hong Meirin",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1661543.jpg",
                    Rating = 9.85,
                    RatingCount = 20,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1661547,
                    Name =
                        "Touhou Project - Moriya Suwako - FumoFumo - Touhou Plush Series  (59) - Ver. 1.5 (Gift)",
                    Character = "Moriya Suwako",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1661547.jpg",
                    Rating = 9.69,
                    RatingCount = 26,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1661553,
                    Name =
                        "Touhou Project - Syameimaru Aya - FumoFumo - Touhou Plush Series  (60) - Fujinroku ver. (Gift)",
                    Character = "Syameimaru Aya",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1661553.jpg",
                    Rating = 9.5,
                    RatingCount = 18,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1661554,
                    Name = "Touhou Project - Junko - FumoFumo - Touhou Plush Series  (61) (Gift)",
                    Character = "Junko",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1661554.jpg",
                    Rating = 9.67,
                    RatingCount = 24,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1779452,
                    Name =
                        "Touhou Project - Izayoi Sakuya - FumoFumo - Touhou Plush Series  (64) - Ver. 1.5 (Gift)",
                    Character = "Izayoi Sakuya",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1779452.jpg",
                    Rating = 9.78,
                    RatingCount = 36,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1808196,
                    Name = "Mascot Character - Amico - FumoFumo (Gift)",
                    Character = "Amico",
                    Origin = "Mascot Character",
                    ThumbnailUrl = "/fumos/1808196.jpg",
                    Rating = 9.69,
                    RatingCount = 35,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1827254,
                    Name =
                        "Touhou Project - Ibuki Suika - FumoFumo - Touhou Plush Series  (66) (Gift)",
                    Character = "Ibuki Suika",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1827254.jpg",
                    Rating = 9.61,
                    RatingCount = 23,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1849001,
                    Name = "Piapro Characters - Hatsune Miku - FumoFumo - NT (AmiAmi, Gift)",
                    Character = "Hatsune Miku",
                    Origin = "Piapro Characters",
                    ThumbnailUrl = "/fumos/1849001.jpg",
                    Rating = 9.8,
                    RatingCount = 30,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1857228,
                    Name =
                        "Touhou Project - Yakumo Yukari - FumoFumo - Touhou Plush Series  (74) - Kourindou ver. (Gift)",
                    Character = "Yakumo Yukari",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1857228.jpg",
                    Rating = 9.75,
                    RatingCount = 16,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1857229,
                    Name =
                        "Touhou Project - Patchouli Knowledge - FumoFumo - Touhou Plush Series  (75) - Kourindou ver. (Gift)",
                    Character = "Patchouli Knowledge",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/1857229.jpg",
                    Rating = 9.64,
                    RatingCount = 14,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 1857233,
                    Name =
                        "Touhou Lost Word - Remilia Scarlet - FumoFumo - Touhou Plush Series  (71) - Tiny Devil Mistress ver. (Gift)",
                    Character = "Remilia Scarlet",
                    Origin = "Touhou Lost Word",
                    ThumbnailUrl = "/fumos/1857233.jpg",
                    Rating = 8.92,
                    RatingCount = 13,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2024552,
                    Name =
                        "Touhou Project - Hakurei Reimu - FumoFumo - Touhou Plush Series  (76) - Yume Jikuu ver. (Gift)",
                    Character = "Hakurei Reimu",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2024552.jpg",
                    Rating = 9.53,
                    RatingCount = 32,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2024560,
                    Name =
                        "Touhou Project - Kirisame Marisa - FumoFumo - Touhou Plush Series  (77) - Yume Jikuu ver. (Gift)",
                    Character = "Kirisame Marisa",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2024560.jpg",
                    Rating = 9.44,
                    RatingCount = 27,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2061380,
                    Name =
                        "Touhou Project - Kaenbyou Rin - FumoFumo - Touhou Plush Series  (79) (Gift)",
                    Character = "Kaenbyou Rin",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2061380.jpg",
                    Rating = 9.64,
                    RatingCount = 33,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2061381,
                    Name =
                        "Touhou Project - Reiuzi Utsuho - FumoFumo - Touhou Plush Series  (80) (Gift)",
                    Character = "Reiuzi Utsuho",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2061381.jpg",
                    Rating = 9.68,
                    RatingCount = 38,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2235030,
                    Name = "Saya no Uta - Saya - FumoFumo (Gift)",
                    Character = "Saya",
                    Origin = "Saya no Uta",
                    ThumbnailUrl = "/fumos/2235030.jpg",
                    Rating = 9.6,
                    RatingCount = 119,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2258554,
                    Name =
                        "Touhou Lost Word - Saigyouzi Yuyuko - FumoFumo - Touhou Plush Series  (87) - Chiisana Bourei Rousho ver. (Gift)",
                    Character = "Saigyouzi Yuyuko",
                    Origin = "Touhou Lost Word",
                    ThumbnailUrl = "/fumos/2258554.jpg",
                    Rating = 9.1,
                    RatingCount = 20,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2265770,
                    Name = "Touhou Project - Nazrin - FumoFumo - Touhou Plush Series  (91) (Gift)",
                    Character = "Nazrin",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2265770.jpg",
                    Rating = 9.89,
                    RatingCount = 19,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2380288,
                    Name =
                        "Piapro Characters - Hatsune Miku - FumoFumo - Senbonzakura ver. (AmiAmi, Gift)",
                    Character = "Hatsune Miku",
                    Origin = "Piapro Characters",
                    ThumbnailUrl = "/fumos/2380288.jpg",
                    Rating = 9.83,
                    RatingCount = 6,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2441674,
                    Name = "Azur Lane - Taihou - FumoFumo (Gift)",
                    Character = "Taihou",
                    Origin = "Azur Lane",
                    ThumbnailUrl = "/fumos/2441674.jpg",
                    Rating = 9.75,
                    RatingCount = 12,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2512285,
                    Name =
                        "Touhou Project - Hiziri Byakuren - FumoFumo - Touhou Plush Series (Gift)",
                    Character = "Hiziri Byakuren",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2512285.jpg",
                    Rating = 8.75,
                    RatingCount = 8,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2622402,
                    Name = "Azur Lane - New Jersey - FumoFumo (Gift)",
                    Character = "New Jersey",
                    Origin = "Azur Lane",
                    ThumbnailUrl = "/fumos/2622402.jpg",
                    Rating = 9.8,
                    RatingCount = 10,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2808806,
                    Name =
                        "Touhou Project - Koakuma - FumoFumo - Touhou Plush Series  (102) (Gift)",
                    Character = "Koakuma",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/2808806.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2907546,
                    Name =
                        "Shin Seiki Evangelion - Ikari Shinji - FumoFumo - Ichinana Size (AmiAmi, Gift)",
                    Character = "Ikari Shinji",
                    Origin = "Shin Seiki Evangelion",
                    ThumbnailUrl = "/fumos/2907546.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 2907938,
                    Name = "Umineko no Naku Koro ni - Beatrice - FumoFumo (Gift, Jast USA)",
                    Character = "Beatrice",
                    Origin = "Umineko no Naku Koro ni",
                    ThumbnailUrl = "/fumos/2907938.jpg",
                    Rating = 9.73,
                    RatingCount = 30,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3088083,
                    Name =
                        "Touhou Project - Onozuka Komachi - FumoFumo - Touhou Plush Series  (108) (Gift)",
                    Character = "Onozuka Komachi",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/3088083.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3088089,
                    Name =
                        "Touhou Project - Doremy Sweet - FumoFumo - Touhou Plush Series  (111) (Gift)",
                    Character = "Doremy Sweet",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/3088089.jpg",
                    Rating = 10.0,
                    RatingCount = 1,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3088092,
                    Name =
                        "Touhou Project - Kaku Seiga - FumoFumo - Touhou Plush Series  (110) (Gift)",
                    Character = "Kaku Seiga",
                    Origin = "Touhou Project",
                    ThumbnailUrl = "/fumos/3088092.jpg",
                    Rating = 10.0,
                    RatingCount = 1,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3128896,
                    Name =
                        "Needy Girl Overdose - Chouzetsu Saikawa Tenshi-chan - FumoFumo (Akiba-Hobby, Gift, Jast USA)",
                    Character = "Chouzetsu Saikawa Tenshi-chan",
                    Origin = "Needy Girl Overdose",
                    ThumbnailUrl = "/fumos/3128896.jpg",
                    Rating = 10.0,
                    RatingCount = 1,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3128898,
                    Name =
                        "Needy Girl Overdose - Ame-chan - FumoFumo (Akiba-Hobby, Gift, Jast USA)",
                    Character = "Ame-chan",
                    Origin = "Needy Girl Overdose",
                    ThumbnailUrl = "/fumos/3128898.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3223935,
                    Name =
                        "Evangelion Shin Gekijouban - Souryuu Asuka Langley - FumoFumo - Ichinana Size (Gift)",
                    Character = "Souryuu Asuka Langley",
                    Origin = "Evangelion Shin Gekijouban",
                    ThumbnailUrl = "/fumos/3223935.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3223940,
                    Name =
                        "Evangelion Shin Gekijouban - Ayanami Rei - FumoFumo - Ichinana Size (Gift)",
                    Character = "Ayanami Rei",
                    Origin = "Evangelion Shin Gekijouban",
                    ThumbnailUrl = "/fumos/3223940.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
                new Fumo
                {
                    MfcId = 3223941,
                    Name =
                        "Evangelion Shin Gekijouban - Nagisa Kaworu - FumoFumo - Ichinana Size (Gift)",
                    Character = "Nagisa Kaworu",
                    Origin = "Evangelion Shin Gekijouban",
                    ThumbnailUrl = "/fumos/3223941.jpg",
                    Rating = 0,
                    RatingCount = 0,
                    WhenAdded = DateTimeOffset.MinValue,
                    LastOrder = DateTimeOffset.MinValue,
                    OrderCount = 0,
                },
            ]);

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

        modelBuilder
            .Entity<TekkenCharacter>()
            .Property(e => e.Image)
            .HasColumnType("bytea");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConversion>();

        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeToDateTimeUtc>();

        configurationBuilder.Properties<byte[]>().HaveColumnType("bytea");
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
