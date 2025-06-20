using MARS.Server.ApplicationState;
using MARS.Server.CustomLoggers.DatabaseLogger;
using MARS.Server.Services._365Genius.Entitys;
using MARS.Server.Services.Framedata.Entitys;
using MARS.Server.Services.RandomMem.Entity;
using MARS.Server.Services.SoundRequest.Entitys;
using MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;
using MARS.Server.Services.Twitch.FumoFriday.Entitys;
using MARS.Server.Services.Twitch.HelloVideos.Entitys;
using MARS.Server.Services.Twitch.Management.Entitys;
using MARS.Server.Services.Twitch.MiniGamesStats.Entitys;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MARS.Server.DataBaseContext;

public sealed class AppDbContext : DbContext
{
    private static readonly Lock Locker = new();
    private static bool _isMigrated;

    public AppDbContext(DbContextOptions<AppDbContext> options, bool isMigrations)
        : base(options)
    {
        if (!_isMigrated && !isMigrations)
        {
            Locker.Enter();
            if (!_isMigrated)
            {
                var migrations = Database.GetPendingMigrations();

                if (migrations.Any())
                {
                    Database.Migrate();
                }

                _isMigrated = true;
            }

            Locker.Exit();
        }
    }

    public DbSet<Host> Hosts { get; set; } = null!;
    public DbSet<Waifu> Waifus { get; set; } = null!;
    public DbSet<TelegramUser> TelegramUsers { get; set; } = null!;
    public DbSet<MediaInfo> Alerts { get; set; } = null!;
    public DbSet<HostCoolDown> HostsCoolDowns { get; set; } = null!;
    public DbSet<HostAutoHello> HostsGreetings { get; set; } = null!;
    public DbSet<AutoMessage> AutoMessages { get; set; } = null!;
    public DbSet<TokenInfo> TwitchToken { get; set; } = null!;
    public DbSet<Log> Logs { get; set; } = null!;
    public DbSet<MemeOrder> RandomMemeOrder { get; set; } = null!;
    public DbSet<MemeType> RandomMemeType { get; set; } = null!;
    public DbSet<FumoUser> FumoUsers { get; set; }
    public DbSet<HelloVideosUsers> HelloVideosUsers { get; set; } = null!;
    public DbSet<Video365> Videos365 { get; set; } = null!;
    public DbSet<TekkenCharacter> TekkenCharacters { get; set; } = null!;
    public DbSet<Move> TekkenMoves { get; set; } = null!;
    public DbSet<TwitchLeaderboardUser> TwitchLeaderboardUsers { get; set; } = null!;
    public DbSet<TelegramUpdateReceiverOffset> TelegramUpdateReceiverOffset { get; set; } = null!;
    public DbSet<WTelegramAlloweedChannel> WTelegramAlloweedChannels { get; set; } = null!;
    public DbSet<RootState> ApplicationState { get; set; } = null!;
    public DbSet<BaseTrackInfo> SoundRequestBaseTrackInfos { get; set; } = null!;
    public DbSet<PlayerState> SoundRequestPlayerState { get; set; } = null!;
    public DbSet<SoundRequestBackgroundTrackId> SoundRequestBackgroundTracks { get; set; } = null!;
    public DbSet<UserRequestedTrack> SoundRequestUserQueue { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
                    new()
                    {
                        Name = "Random Sound",
                        Id = 3,
                        FolderPath = "Alerts\\zvik",
                    },
                    new()
                    {
                        Name = "Random Meme",
                        Id = 2,
                        FolderPath = "Alerts\\random_meme",
                    },
                ]
            );

        modelBuilder
            .Entity<UserRequestedTrack>()
            .HasOne(urt => urt.RequestedTrack)
            .WithOne()
            .HasForeignKey<UserRequestedTrack>(urt => urt.RequestedTrackId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<SoundRequestBackgroundTrackId>()
            .HasOne(e => e.BaseTrackInfo)
            .WithOne()
            .HasForeignKey<SoundRequestBackgroundTrackId>(e => e.TrackId);

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
                    metaInfo.Property(p => p.VIP).HasColumnName("MetaInfo_VIP");
                    metaInfo.Property(e => e.Priority).HasColumnName("MetaInfo_Priority");
                }
            );

            entity.OwnsOne(
                e => e.StylesInfo,
                metaInfo =>
                {
                    metaInfo.Property(p => p.IsBorder).HasColumnName("StylesInfo_IsBorder");
                }
            );
        });

        modelBuilder.Entity<Move>().HasKey(o => new { o.CharacterName, o.Command });

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

        modelBuilder
            .Entity<RootState>()
            .HasData(new RootState() { Id = 1, RandomMemeOnlineIsStop = false });
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
            c => c
        );
}
