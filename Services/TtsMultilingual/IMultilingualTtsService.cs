namespace MARS.Server.Services.TtsMultilingual;

public interface IMultilingualTtsService
{
    Task<OperationResult<MultilingualTtsAudioResult>> SynthesizeAsync(
        string text,
        string? language,
        string? speaker,
        CancellationToken cancellationToken = default
    );
}