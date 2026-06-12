using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MARS.Server.Services.Twitch.TwitchFollowers;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Models;

namespace MARS.Server.Services.Twitch;

/// <summary>
/// Service that guarantees a <see cref="TwitchUser"/> exists in the database.
/// It follows a Get‑Or‑Create pattern and can enrich the user data via the Twitch API.
/// All dependencies are optional so the class can be easily mocked in unit tests.
/// </summary>
public class TwitchUserEnsureService : ITwitchUserEnsureService
{
    // ----- Dependencies -----------------------------------------------------
    private readonly IDbContextFactory<AppDbContext>? _dbFactory;
    private readonly TwitchUserInfoService? _userInfoService;
    private readonly TokenService? _tokenService;
    private readonly ITwitchAPI? _api;
    private readonly ILogger<TwitchUserEnsureService>? _logger;

    /// <summary>
    /// Primary constructor used by production code. All parameters are optional
    /// allowing the service to be instantiated without providing real dependencies –
    /// this is required for Moq to create a proxy of the concrete class.
    /// </summary>
    public TwitchUserEnsureService(
        IDbContextFactory<AppDbContext>? dbFactory = null,
        TwitchUserInfoService? userInfoService = null,
        TokenService? tokenService = null,
        ITwitchAPI? api = null,
        ILogger<TwitchUserEnsureService>? logger = null
    )
    {
        _dbFactory = dbFactory;
        _userInfoService = userInfoService;
        _tokenService = tokenService;
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Parameterless constructor required for Moq proxy generation. It simply forwards
    /// to the primary constructor with null arguments.
    /// </summary>
    public TwitchUserEnsureService()
        : this(null) { }

    // ----- Public API ------------------------------------------------------
    // All public methods are virtual so that tests can override them with Moq.

    /// <inheritdoc />
    public virtual async Task<TwitchUser> EnsureUserExistsAsync(
        ChatMessage chatMessage,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser =
            TwitchUser.FromChatMessage(chatMessage)
            ?? throw new ArgumentException("Invalid ChatMessage data", nameof(chatMessage));
        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TwitchUser> EnsureUserExistsAsync(
        string twitchId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(twitchId))
        {
            throw new ArgumentException("User not found", nameof(twitchId));
        }

        // Try to locate an existing record.
        if (_dbFactory != null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var existing = await db
                .TwitchUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TwitchId == twitchId, cancellationToken);
            if (existing != null)
            {
                return existing;
            }
        }

        // Fallback – request data from the Twitch API and create the user.
        if (_api != null && _tokenService?.Token?.AccessToken != null)
        {
            var response = await _api.Helix.Users.GetUsersAsync(
                [twitchId],
                null,
                _tokenService.Token.AccessToken
            );
            if (response?.Users?.Length > 0)
            {
                var twitchUser =
                    TwitchUser.FromUser(response.Users.First())
                    ?? throw new ArgumentException("Invalid TwitchId", nameof(twitchId));
                return await EnsureUserExistsAsync(twitchUser, cancellationToken);
            }
        }

        throw new ArgumentException("User not found", nameof(twitchId));
    }

    /// <inheritdoc />
    public virtual async Task<OperationResult> EnsureUsersExistsAsync(
        List<string>? twitchIds,
        CancellationToken cancellationToken = default
    )
    {
        if (twitchIds == null || twitchIds.Count == 0)
        {
            return OperationResult.Bad("Список ID пользователей пуст");
        }

        var result = OperationResult.Ok();
        foreach (var chunk in twitchIds.Chunk(100))
        {
            try
            {
                var response = await _api!.Helix.Users.GetUsersAsync(
                    chunk.ToList(),
                    null,
                    _tokenService!.Token?.AccessToken
                );
                if (response?.Users?.Length > 0)
                {
                    foreach (var user in response.Users)
                    {
                        var twitchUser = TwitchUser.FromUser(user);
                        await EnsureUserExistsAsync(twitchUser, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Ошибка при обработке чанка пользователей Twitch (размер: {ChunkSize})",
                    chunk.Length
                );
                result = OperationResult.Bad($"Ошибка при обработке пользователей: {ex.Message}");
            }
        }
        return result;
    }

    /// <inheritdoc />
    public virtual async Task<TwitchUser> EnsureUserExistsAsync(
        OnMessageReceivedArgs args,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser =
            TwitchUser.FromOnMessageReceivedArgs(args)
            ?? throw new ArgumentException("Invalid OnMessageReceivedArgs data", nameof(args));
        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TwitchUser> EnsureUserExistsAsync(
        ChannelPointsCustomRewardRedemptionArgs args,
        CancellationToken cancellationToken = default
    )
    {
        var twitchUser = TwitchUser.FromChannelPointsCustomRewardRedemptionArgs(args);
        ArgumentNullException.ThrowIfNull(twitchUser);
        return await EnsureUserExistsAsync(twitchUser, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TwitchUser> EnsureUserExistsAsync(
        TwitchUser? twitchUser,
        CancellationToken cancellationToken = default
    )
    {
        if (twitchUser == null || string.IsNullOrWhiteSpace(twitchUser.TwitchId))
        {
            return null!; // callers treat null as not‑found.
        }

        try
        {
            if (_dbFactory != null)
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                var existing = await db.TwitchUsers.FindAsync(
                    [twitchUser.TwitchId],
                    cancellationToken: cancellationToken
                );
                if (existing != null)
                {
                    // Update mutable fields.
                    existing.UserLogin = twitchUser.UserLogin;
                    existing.DisplayName = twitchUser.DisplayName;
                    existing.ProfileImageUrl =
                        twitchUser.ProfileImageUrl ?? existing.ProfileImageUrl;
                    existing.ChatColor = twitchUser.ChatColor ?? existing.ChatColor;
                    existing.IsModerator = twitchUser.IsModerator;
                    existing.IsVip = twitchUser.IsVip;
                    existing.LastUpdated = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    _logger?.LogInformation(
                        "Обновлен пользователь Twitch: {UserName} (ID: {UserId})",
                        existing.UserLogin,
                        existing.TwitchId
                    );
                    return existing;
                }
                // Not existing – enrich then insert.
                var enriched = await EnrichUserDataFromApiAsync(twitchUser);
                db.TwitchUsers.Add(enriched);
                await db.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation(
                    "Создан новый пользователь Twitch: {UserName} (ID: {UserId}), Avatar: {Avatar}",
                    enriched.UserLogin,
                    enriched.TwitchId,
                    enriched.ProfileImageUrl ?? "null"
                );
                return enriched;
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true)
        {
            // Race condition – another thread created the record concurrently.
            _logger?.LogInformation(
                "Конфликт при создании пользователя {TwitchId}. Получение существующего пользователя.",
                twitchUser.TwitchId
            );
            if (_dbFactory != null)
            {
                await using var dbRetry = await _dbFactory.CreateDbContextAsync(cancellationToken);
                var created = await dbRetry.TwitchUsers.FirstOrDefaultAsync(
                    u => u.TwitchId == twitchUser.TwitchId,
                    cancellationToken
                );
                if (created != null)
                {
                    return created;
                }
            }
            throw new InvalidOperationException(
                $"Не удалось получить пользователя {twitchUser.TwitchId} после constraint violation"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogException(ex);
        }
        return null!;
    }

    // ----- Private helpers -------------------------------------------------
    private async Task<TwitchUser> EnrichUserDataFromApiAsync(TwitchUser twitchUser)
    {
        if (_tokenService?.Token?.AccessToken == null)
        {
            return twitchUser;
        }

        try
        {
            var userInfoTask = _userInfoService!.GetUserInfoAsync(twitchUser.TwitchId);
            var chatColorTask = _userInfoService!.GetUserChatColorAsync(twitchUser.TwitchId);
            await Task.WhenAll(userInfoTask, chatColorTask);
            var apiUser = await userInfoTask;
            var chatColor = await chatColorTask;
            if (twitchUser.ProfileImageUrl == null && apiUser?.ProfileImageUrl != null)
            {
                twitchUser.ProfileImageUrl = apiUser.ProfileImageUrl;
            }

            if (twitchUser.ChatColor == null && chatColor != null)
            {
                twitchUser.ChatColor = chatColor;
            }

            if (apiUser != null)
            {
                if (twitchUser.UserLogin.StartsWith("user_"))
                {
                    twitchUser.UserLogin = apiUser.Login ?? twitchUser.UserLogin;
                }

                if (twitchUser.DisplayName.StartsWith("User"))
                {
                    twitchUser.DisplayName = apiUser.DisplayName ?? twitchUser.DisplayName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Не удалось обогатить данные из API для пользователя {TwitchId}",
                twitchUser.TwitchId
            );
        }
        return twitchUser;
    }
}
