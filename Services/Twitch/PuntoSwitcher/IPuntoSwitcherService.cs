namespace MARS.Server.Services.Twitch.PuntoSwitcher;

public interface IPuntoSwitcherService
{
    bool IsFilterEnabled { get; set; }
    OperationResult<PuntoSwitchSuggestion> TryFixMessage(string? message);
}
