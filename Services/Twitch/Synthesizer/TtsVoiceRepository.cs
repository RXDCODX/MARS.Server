using MARS.Server.Services.Twitch.Synthesizer.Enitity;

namespace MARS.Server.Services.Twitch.Synthesizer;

public class TtsVoiceRepository(IDbContextFactory<AppDbContext> contextFactory)
    : ITtsVoiceRepository
{
    public async Task<List<string>> GetBlockedVoicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context
            .BlockedTtsVoices.AsNoTracking()
            .Select(v => v.VoiceName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AddBlockedVoiceAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedVoice = Normalize(voiceName);
        if (string.IsNullOrWhiteSpace(normalizedVoice))
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var alreadyExists = await context.BlockedTtsVoices.AnyAsync(
            v => EF.Functions.ILike(v.VoiceName, normalizedVoice),
            cancellationToken
        );

        if (alreadyExists)
        {
            return false;
        }

        context.BlockedTtsVoices.Add(new BlockedTtsVoice { VoiceName = normalizedVoice });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveBlockedVoiceAsync(
        string voiceName,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedVoice = Normalize(voiceName);
        if (string.IsNullOrWhiteSpace(normalizedVoice))
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.BlockedTtsVoices.FirstOrDefaultAsync(
            v => EF.Functions.ILike(v.VoiceName, normalizedVoice),
            cancellationToken
        );

        if (entity is null)
        {
            return false;
        }

        context.BlockedTtsVoices.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string Normalize(string voiceName)
    {
        return voiceName.Trim().ToLowerInvariant();
    }
}
