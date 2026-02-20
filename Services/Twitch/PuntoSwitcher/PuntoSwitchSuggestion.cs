namespace MARS.Server.Services.Twitch.PuntoSwitcher;

public class PuntoSwitchSuggestion
{
    public string OriginalMessage { get; init; } = string.Empty;
    public string CorrectedMessage { get; init; } = string.Empty;
    public int ReplacedTokens { get; init; }
    public bool HasChanges => ReplacedTokens > 0;
}
