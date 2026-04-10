using MARS.Server.Services.CommandExecutor.Entitys;
using MARS.Server.Services.CommandExecutor.Entitys.Commands;
using MARS.Server.Services.Twitch.Entitys;
using MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward;
using MARS.Server.Services.Twitch.Rewards.TwitchMikuMondayReward.Entities;

namespace MARS.Server.Services.CommandExecutor.Commands;

public class MikumondayMikuMondayRewardCommand(
    MikuMondayTracksService tracksService,
    IHubContext<TelegramusHub, ITelegramusHub> hubContext,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<MikuMondayRewardCommand> logger
) : BaseCommand
{
    public override string CommandName => "mikumonday";
    public override string Description =>
        "Ручная выдача Miku Monday: без очереди, по нику (или стример без параметра)";
    public override bool IsAdminCommand => true;

    public override Platform[] AvailablePlatforms =>
        [Platform.Telegram, Platform.Api, Platform.Twitch];

    public override CommandParameterInfo[] Parameters =>
        [
            new()
            {
                Name = "nickname",
                Description = "Никнейм пользователя Twitch. Если не указан — считается стримером",
                Type = "string",
                Required = false,
            },
        ];

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object> parameters,
        Platform platform = Platform.None,
        CancellationToken cancellationToken = default
    )
    {
        var result = "Не удалось выдать трек.";

        var nickname = parameters.TryGetValue("nickname", out var nameObj)
            ? nameObj?.ToString()?.Trim()
            : null;

        var isStreamerCall = string.IsNullOrWhiteSpace(nickname);
        var sanitizedNickname = nickname?.TrimStart('@');

        var login = isStreamerCall ? TwitchExstension.Channel : sanitizedNickname ?? string.Empty;
        var displayName = isStreamerCall
            ? TwitchExstension.Channel
            : sanitizedNickname ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        TwitchUser? twUser;
        if (isStreamerCall)
        {
            twUser = await db
                .TwitchUsers.AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.TwitchId == TwitchExstension.ChannelId,
                    cancellationToken
                );
        }
        else
        {
            twUser = await db
                .TwitchUsers.AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        EF.Functions.Like(u.UserLogin, login)
                        || EF.Functions.Like(u.DisplayName, displayName),
                    cancellationToken
                );
        }

        if (twUser is not null)
        {
            var trackResult = await tracksService.GetRandomTrackForStreamerAsync();

            if (!string.IsNullOrWhiteSpace(trackResult.Error))
            {
                logger.LogWarning(
                    "Miku Monday manual: ошибка для {User}: {Error}",
                    displayName,
                    trackResult.Error
                );
                result = trackResult.Error;
            }
            else if (trackResult.Track == null)
            {
                logger.LogError(
                    "Miku Monday manual: не удалось получить трек для {User}",
                    displayName
                );
                result = "Не удалось получить трек.";
            }
            else
            {
                var selectedTrackDto = new MikuTrackDto
                {
                    Id = trackResult.Track.BaseTrackInfoId,
                    Number = trackResult.Track.Number,
                    Artist =
                        trackResult.Track.BaseTrackInfo?.Authors?.FirstOrDefault()
                        ?? "Unknown Artist",
                    Title = trackResult.Track.BaseTrackInfo?.TrackName ?? "Unknown Title",
                    Url = trackResult.Track.BaseTrackInfo?.Url.ToString() ?? string.Empty,
                    ThumbnailUrl = trackResult.Track.BaseTrackInfo?.ArtworkUrl?.ToString(),
                };

                var availableTracksDto = trackResult
                    .AvailableTracks.Select(t => new MikuTrackDto
                    {
                        Id = t.BaseTrackInfoId,
                        Number = t.Number,
                        Artist = t.BaseTrackInfo?.Authors?.FirstOrDefault() ?? "Unknown Artist",
                        Title = t.BaseTrackInfo?.TrackName ?? "Unknown Title",
                        Url = t.BaseTrackInfo?.Url.ToString() ?? string.Empty,
                        ThumbnailUrl = t.BaseTrackInfo?.ArtworkUrl?.ToString(),
                    })
                    .ToList();

                var mikuMondayData = new MikuMondayDto
                {
                    Id = Guid.NewGuid(),
                    TwitchUser = twUser,
                    SelectedTrack = selectedTrackDto,
                    AvailableTracks = availableTracksDto,
                    SkipAvailableTracksUpdate = isStreamerCall,
                };

                await hubContext.Clients.All.MikuMonday(mikuMondayData);

                var who = isStreamerCall ? "стример" : "пользователь";
                var trackArtist = selectedTrackDto.Artist;
                var trackTitle = selectedTrackDto.Title;

                logger.LogInformation(
                    "Miku Monday manual: трек #{Number} {Artist} - {Title} выдан для {Who} {User}",
                    selectedTrackDto.Number,
                    trackArtist,
                    trackTitle,
                    who,
                    displayName
                );

                result =
                    $"Выдан трек #{selectedTrackDto.Number}: {trackArtist} - {trackTitle} для {who} @{displayName}";
            }
        }
        else
        {
            result = $"Пользователь @{displayName} не найден в базе TwitchUsers.";
        }

        return result;
    }
}
