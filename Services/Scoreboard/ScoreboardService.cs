using MARS.Server.Services.Scoreboard.Entitys;

namespace MARS.Server.Services.Scoreboard;

public class ScoreboardService(
    IDbContextFactory<AppDbContext> factory,
    ILogger<ScoreboardService> logger
)
{
    // Статический словарь для отслеживания отложенных обновлений
    private static readonly Dictionary<
        string,
        (ScoreboardDto State, System.Threading.Timer Timer)
    > PendingUpdates = [];

    private static readonly SemaphoreSlim SemaphoreSlim = new(1);
    private const int DebounceDelayMs = 500; // 500ms задержка для группировки изменений

    public async Task<ScoreboardDto> GetCurrentStateAsync()
    {
        await using var context = await factory.CreateDbContextAsync();

        var state = await context
            .ScoreboardStates.Include(s => s.Players)
            .Include(s => s.Layout)
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        return state == null ? CreateDefaultState() : MapToDto(state);
    }

    public async Task<ScoreboardDto> UpdateStateAsync(ScoreboardDto dto)
    {
        // Генерируем уникальный ключ для этого обновления
        var updateKey = Guid.NewGuid().ToString();

        await SemaphoreSlim.WaitAsync();
        // Отменяем предыдущий таймер, если он существует
        if (PendingUpdates.TryGetValue(updateKey, out var existing))
        {
            await existing.Timer.DisposeAsync();
        }

        // Создаем новый таймер для отложенного обновления
        var timer = new System.Threading.Timer(
            async _ => await ProcessDebouncedUpdate(updateKey),
            null,
            DebounceDelayMs,
            Timeout.Infinite
        );
        PendingUpdates[updateKey] = (dto, timer);
        SemaphoreSlim.Release();

        // Возвращаем текущее состояние немедленно
        return await GetCurrentStateAsync() ?? dto;
    }

    private async Task ProcessDebouncedUpdate(string updateKey)
    {
        ScoreboardDto? stateToUpdate = null;

        await SemaphoreSlim.WaitAsync();

        if (PendingUpdates.TryGetValue(updateKey, out var pending))
        {
            stateToUpdate = pending.State;
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
        await using var context = await factory.CreateDbContextAsync();

        // Деактивируем предыдущее состояние
        var previousStates = await context.ScoreboardStates.Where(s => s.IsActive).ToListAsync();

        foreach (var state in previousStates)
        {
            state.IsActive = false;
            state.UpdatedAt = DateTime.UtcNow;
        }

        // Создаем новое состояние
        var newState = new ScoreboardState
        {
            Title = dto.Meta.Title,
            FightRule = dto.Meta.FightRule,
            MainColor = dto.Colors.MainColor,
            PlayerNamesColor = dto.Colors.PlayerNamesColor,
            TournamentTitleColor = dto.Colors.TournamentTitleColor,
            FightModeColor = dto.Colors.FightModeColor,
            ScoreColor = dto.Colors.ScoreColor,
            BackgroundColor = dto.Colors.BackgroundColor,
            BorderColor = dto.Colors.BorderColor,
            IsVisible = dto.IsVisible,
            AnimationDuration = dto.AnimationDuration,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        // Добавляем игроков
        newState.Players.Add(
            new ScoreboardPlayer
            {
                Name = dto.Player1.Name,
                Sponsor = dto.Player1.Sponsor,
                Score = dto.Player1.Score,
                Tag = dto.Player1.Tag,
                Flag = dto.Player1.Flag,
                Final = dto.Player1.Final,
                Position = 1,
            }
        );

        newState.Players.Add(
            new ScoreboardPlayer
            {
                Name = dto.Player2.Name,
                Sponsor = dto.Player2.Sponsor,
                Score = dto.Player2.Score,
                Tag = dto.Player2.Tag,
                Flag = dto.Player2.Flag,
                Final = dto.Player2.Final,
                Position = 2,
            }
        );

        // Добавляем настройки макета, если они есть
        if (dto.Layout != null)
        {
            newState.Layout = new ScoreboardLayout
            {
                HeaderTop = dto.Layout.HeaderTop,
                HeaderLeft = dto.Layout.HeaderLeft,
                PlayersTop = dto.Layout.PlayersTop,
                PlayersLeft = dto.Layout.PlayersLeft,
                PlayersRight = dto.Layout.PlayersRight,
                HeaderHeight = dto.Layout.HeaderHeight,
                HeaderWidth = dto.Layout.HeaderWidth,
                PlayerBarHeight = dto.Layout.PlayerBarHeight,
                PlayerBarWidth = dto.Layout.PlayerBarWidth,
                ScoreSize = dto.Layout.ScoreSize,
                FlagSize = dto.Layout.FlagSize,
                Spacing = dto.Layout.Spacing,
                Padding = dto.Layout.Padding,
                ShowHeader = dto.Layout.ShowHeader,
                ShowFlags = dto.Layout.ShowFlags,
                ShowSponsors = dto.Layout.ShowSponsors,
                ShowTags = dto.Layout.ShowTags,
            };
        }

        context.ScoreboardStates.Add(newState);
        await context.SaveChangesAsync();

        logger.LogInformation("Scoreboard state updated: {Title}", newState.Title);

        return MapToDto(newState);
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
        await using var context = await factory.CreateDbContextAsync();

        var currentState = await context
            .ScoreboardStates.Where(s => s.IsActive)
            .FirstOrDefaultAsync();

        if (currentState == null)
        {
            return false;
        }

        currentState.IsVisible = isVisible;
        currentState.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Scoreboard visibility set to: {IsVisible}", isVisible);
        return true;
    }

    public async Task<bool> UpdatePlayerScoreAsync(int playerPosition, int newScore)
    {
        await using var context = await factory.CreateDbContextAsync();

        var player = await context
            .ScoreboardPlayers.Include(p => p.ScoreboardState)
            .Where(p => p.ScoreboardState.IsActive && p.Position == playerPosition)
            .FirstOrDefaultAsync();

        if (player == null)
        {
            return false;
        }

        player.Score = newScore;
        player.ScoreboardState.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Player {Position} score updated to: {Score}",
            playerPosition,
            newScore
        );
        return true;
    }

    public async Task<bool> SetPlayerFinalAsync(int playerPosition, string final)
    {
        await using var context = await factory.CreateDbContextAsync();

        var player = await context
            .ScoreboardPlayers.Include(p => p.ScoreboardState)
            .Where(p => p.ScoreboardState.IsActive && p.Position == playerPosition)
            .FirstOrDefaultAsync();

        if (player == null)
        {
            return false;
        }

        player.Final = final;
        player.ScoreboardState.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Player {Position} final status set to: {Final}",
            playerPosition,
            final
        );
        return true;
    }

    public async Task<List<ScoreboardDto>> GetHistoryAsync(int count = 10)
    {
        await using var context = await factory.CreateDbContextAsync();

        var states = await context
            .ScoreboardStates.Include(s => s.Players)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync();

        return [.. states.Select(MapToDto)];
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
