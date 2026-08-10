using MARS.Server.DataBaseContext;
using MARS.Server.Services.Scoreboard.Entitys;
using Microsoft.EntityFrameworkCore;
using Timer = System.Timers.Timer;

namespace MARS.Server.Services.Scoreboard;

public class ScoreboardService(
    IDbContextFactory<AppDbContext> factory,
    ILogger<ScoreboardService> logger
)
{
    // Статический словарь для отслеживания отложенных обновлений
    private static readonly Dictionary<string, (ScoreboardDto State, Timer Timer)> PendingUpdates =
        new();

    private static readonly SemaphoreSlim SemaphoreSlim = new(1);
    private static readonly SemaphoreSlim StateSlim = new(1);
    private const int DebounceDelayMs = 500; // 500ms задержка для группировки изменений

    public async Task<ScoreboardDto> GetCurrentStateAsync()
    {
        ScoreboardDto result = CreateDefaultState();

        await StateSlim.WaitAsync();

        await using var context = await factory.CreateDbContextAsync();

        var state = await context
            .ScoreboardStates.AsNoTracking()
            .Include(s => s.Players)
            .Include(s => s.Layout)
            .SingleOrDefaultAsync();

        StateSlim.Release();

        if (state != null)
        {
            result = MapToDto(state);
        }

        return result;
    }

    public async Task<ScoreboardDto> UpdateStateAsync(ScoreboardDto? dto)
    {
        ScoreboardDto result = CreateDefaultState();

        if (dto != null)
        {
            // Используем единый ключ, чтобы обновления схлопывались в одну операцию
            const string updateKey = "scoreboard";

            await SemaphoreSlim.WaitAsync();
            // Отменяем предыдущий таймер, если он существует
            if (PendingUpdates.TryGetValue(updateKey, out var existing))
            {
                existing.Timer.Stop();
                existing.Timer.Dispose();
            }

            // Создаем новый таймер для отложенного обновления (System.Timers.Timer)
            var timer = new Timer(DebounceDelayMs) { AutoReset = false };
            timer.Elapsed += async (s, e) => await ProcessDebouncedUpdate(updateKey);
            timer.Start();
            PendingUpdates[updateKey] = (dto, timer);
            SemaphoreSlim.Release();

            // Возвращаем текущее состояние немедленно
            result = await GetCurrentStateAsync();
        }

        return result;
    }

    private async Task ProcessDebouncedUpdate(string updateKey)
    {
        ScoreboardDto? stateToUpdate = null;

        await SemaphoreSlim.WaitAsync();

        if (PendingUpdates.TryGetValue(updateKey, out var pending))
        {
            stateToUpdate = pending.State;
            pending.Timer.Stop();
            pending.Timer.Dispose();
            PendingUpdates.Remove(updateKey);
        }

        SemaphoreSlim.Release();

        if (stateToUpdate != null)
        {
            try
            {
                await PerformActualUpdateAsync(stateToUpdate);
                logger.LogInformation("Debounced scoreboard state update processed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing debounced update");
            }
        }
    }

    private async Task<ScoreboardDto> PerformActualUpdateAsync(ScoreboardDto dto)
    {
        ScoreboardDto result = CreateDefaultState();

        if (dto != null)
        {
            await using var context = await factory.CreateDbContextAsync();

            // Получаем существующее состояние (единственная запись) или создаем новое
            var state = await context
                .ScoreboardStates.Include(s => s.Players)
                .Include(s => s.Layout)
                .SingleOrDefaultAsync();

            if (state == null)
            {
                state = new ScoreboardState { CreatedAt = DateTime.Now };
                context.ScoreboardStates.Add(state);
            }

            // Обновляем поля состояния
            state.Title = dto.Meta.Title;
            state.FightRule = dto.Meta.FightRule;
            state.MainColor = dto.Colors.MainColor;
            state.PlayerNamesColor = dto.Colors.PlayerNamesColor;
            state.TournamentTitleColor = dto.Colors.TournamentTitleColor;
            state.FightModeColor = dto.Colors.FightModeColor;
            state.ScoreColor = dto.Colors.ScoreColor;
            state.BackgroundColor = dto.Colors.BackgroundColor;
            state.BorderColor = dto.Colors.BorderColor;
            state.IsVisible = dto.IsVisible;
            state.AnimationDuration = dto.AnimationDuration;
            state.UpdatedAt = DateTime.Now;
            state.IsActive = true;

            // Игрок 1
            var player1 = state.Players.FirstOrDefault(p => p.Position == 1);
            if (player1 == null)
            {
                player1 = new ScoreboardPlayer { Position = 1 };
                state.Players.Add(player1);
            }
            player1.Name = dto.Player1.Name;
            player1.Sponsor = dto.Player1.Sponsor;
            player1.Score = dto.Player1.Score;
            player1.Tag = dto.Player1.Tag;
            player1.Flag = dto.Player1.Flag;
            player1.Final = dto.Player1.Final;

            // Игрок 2
            var player2 = state.Players.FirstOrDefault(p => p.Position == 2);
            if (player2 == null)
            {
                player2 = new ScoreboardPlayer { Position = 2 };
                state.Players.Add(player2);
            }
            player2.Name = dto.Player2.Name;
            player2.Sponsor = dto.Player2.Sponsor;
            player2.Score = dto.Player2.Score;
            player2.Tag = dto.Player2.Tag;
            player2.Flag = dto.Player2.Flag;
            player2.Final = dto.Player2.Final;

            // Лейаут
            if (dto.Layout != null)
            {
                state.Layout ??= new ScoreboardLayout();

                state.Layout.HeaderTop = dto.Layout.HeaderTop;
                state.Layout.HeaderLeft = dto.Layout.HeaderLeft;
                state.Layout.PlayersTop = dto.Layout.PlayersTop;
                state.Layout.PlayersLeft = dto.Layout.PlayersLeft;
                state.Layout.PlayersRight = dto.Layout.PlayersRight;
                state.Layout.HeaderHeight = dto.Layout.HeaderHeight;
                state.Layout.HeaderWidth = dto.Layout.HeaderWidth;
                state.Layout.PlayerBarHeight = dto.Layout.PlayerBarHeight;
                state.Layout.PlayerBarWidth = dto.Layout.PlayerBarWidth;
                state.Layout.ScoreSize = dto.Layout.ScoreSize;
                state.Layout.FlagSize = dto.Layout.FlagSize;
                state.Layout.Spacing = dto.Layout.Spacing;
                state.Layout.Padding = dto.Layout.Padding;
                state.Layout.ShowHeader = dto.Layout.ShowHeader;
                state.Layout.ShowFlags = dto.Layout.ShowFlags;
                state.Layout.ShowSponsors = dto.Layout.ShowSponsors;
                state.Layout.ShowTags = dto.Layout.ShowTags;
            }

            await context.SaveChangesAsync();

            logger.LogInformation("Scoreboard state updated: {Title}", state.Title);

            result = MapToDto(state);
        }

        return result;
    }

    public async Task ForceProcessPendingUpdates()
    {
        var updateKeys = new List<string>();

        await SemaphoreSlim.WaitAsync();
        updateKeys.AddRange(PendingUpdates.Keys);
        SemaphoreSlim.Release();

        foreach (var updateKey in updateKeys)
        {
            await ProcessDebouncedUpdate(updateKey);
        }

        logger.LogInformation("Forced processing of {Count} pending updates", updateKeys.Count);
    }

    public async Task<bool> SetVisibilityAsync(bool isVisible)
    {
        var result = false;

        await using var context = await factory.CreateDbContextAsync();

        var currentState = await context.ScoreboardStates.SingleOrDefaultAsync();

        if (currentState != null)
        {
            currentState.IsVisible = isVisible;
            currentState.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            logger.LogInformation("Scoreboard visibility set to: {IsVisible}", isVisible);
            result = true;
        }
        else
        {
            var newState = new ScoreboardState
            {
                IsVisible = isVisible,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsActive = true,
            };

            context.ScoreboardStates.Add(newState);
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Scoreboard state created with visibility: {IsVisible}",
                isVisible
            );
            result = true;
        }

        return result;
    }

    public async Task<bool> UpdatePlayerScoreAsync(int playerPosition, int newScore)
    {
        var result = false;

        if (playerPosition > 0)
        {
            await using var context = await factory.CreateDbContextAsync();

            var currentStateId = await context
                .ScoreboardStates.Select(s => s.Id)
                .SingleOrDefaultAsync();

            if (currentStateId != 0)
            {
                var player = await context
                    .ScoreboardPlayers.Include(p => p.ScoreboardState)
                    .Where(p =>
                        p.ScoreboardStateId == currentStateId && p.Position == playerPosition
                    )
                    .FirstOrDefaultAsync();

                if (player != null)
                {
                    player.Score = newScore;
                    player.ScoreboardState.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    logger.LogInformation(
                        "Player {Position} score updated to: {Score}",
                        playerPosition,
                        newScore
                    );
                    result = true;
                }
            }
        }

        return result;
    }

    public async Task<bool> SetPlayerFinalAsync(int playerPosition, string final)
    {
        var result = false;

        if (playerPosition > 0 && !string.IsNullOrWhiteSpace(final))
        {
            await using var context = await factory.CreateDbContextAsync();

            var currentStateId = await context
                .ScoreboardStates.Select(s => s.Id)
                .SingleOrDefaultAsync();

            if (currentStateId != 0)
            {
                var player = await context
                    .ScoreboardPlayers.Include(p => p.ScoreboardState)
                    .Where(p =>
                        p.ScoreboardStateId == currentStateId && p.Position == playerPosition
                    )
                    .FirstOrDefaultAsync();

                if (player != null)
                {
                    player.Final = final;
                    player.ScoreboardState.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    logger.LogInformation(
                        "Player {Position} final status set to: {Final}",
                        playerPosition,
                        final
                    );
                    result = true;
                }
            }
        }

        return result;
    }

    private static ScoreboardDto CreateDefaultState()
    {
        return new ScoreboardDto
        {
            Player1 = new ScoreboardPlayerDto
            {
                Name = "Player 1",
                Sponsor = "",
                Score = 0,
                Tag = "",
                Flag = "",
                Final = "none",
            },
            Player2 = new ScoreboardPlayerDto
            {
                Name = "Player 2",
                Sponsor = "",
                Score = 0,
                Tag = "",
                Flag = "",
                Final = "none",
            },
            Meta = new ScoreboardMetaDto { Title = "Tournament", FightRule = "Grand Finals" },
            Colors = new ScoreboardColorsDto(),
            IsVisible = true,
            AnimationDuration = 800,
            Layout = new ScoreboardLayoutDto(),
        };
    }

    private static ScoreboardDto MapToDto(ScoreboardState state)
    {
        var player1 = state.Players.FirstOrDefault(p => p.Position == 1);
        var player2 = state.Players.FirstOrDefault(p => p.Position == 2);

        return new ScoreboardDto
        {
            Player1 = new ScoreboardPlayerDto
            {
                Name = player1?.Name ?? "",
                Sponsor = player1?.Sponsor ?? "",
                Score = player1?.Score ?? 0,
                Tag = player1?.Tag ?? "",
                Flag = player1?.Flag ?? "",
                Final = player1?.Final ?? "none",
            },
            Player2 = new ScoreboardPlayerDto
            {
                Name = player2?.Name ?? "",
                Sponsor = player2?.Sponsor ?? "",
                Score = player2?.Score ?? 0,
                Tag = player2?.Tag ?? "",
                Flag = player2?.Flag ?? "",
                Final = player2?.Final ?? "none",
            },
            Meta = new ScoreboardMetaDto { Title = state.Title, FightRule = state.FightRule },
            Colors = new ScoreboardColorsDto
            {
                MainColor = state.MainColor,
                PlayerNamesColor = state.PlayerNamesColor,
                TournamentTitleColor = state.TournamentTitleColor,
                FightModeColor = state.FightModeColor,
                ScoreColor = state.ScoreColor,
                BackgroundColor = state.BackgroundColor,
                BorderColor = state.BorderColor,
            },
            IsVisible = state.IsVisible,
            AnimationDuration = state.AnimationDuration,
            Layout =
                state.Layout != null
                    ? new ScoreboardLayoutDto
                    {
                        HeaderTop = state.Layout.HeaderTop,
                        HeaderLeft = state.Layout.HeaderLeft,
                        PlayersTop = state.Layout.PlayersTop,
                        PlayersLeft = state.Layout.PlayersLeft,
                        PlayersRight = state.Layout.PlayersRight,
                        HeaderHeight = state.Layout.HeaderHeight,
                        HeaderWidth = state.Layout.HeaderWidth,
                        PlayerBarHeight = state.Layout.PlayerBarHeight,
                        PlayerBarWidth = state.Layout.PlayerBarWidth,
                        ScoreSize = state.Layout.ScoreSize,
                        FlagSize = state.Layout.FlagSize,
                        Spacing = state.Layout.Spacing,
                        Padding = state.Layout.Padding,
                        ShowHeader = state.Layout.ShowHeader,
                        ShowFlags = state.Layout.ShowFlags,
                        ShowSponsors = state.Layout.ShowSponsors,
                        ShowTags = state.Layout.ShowTags,
                    }
                    : new ScoreboardLayoutDto(),
        };
    }
}
