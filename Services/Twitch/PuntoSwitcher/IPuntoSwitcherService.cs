namespace MARS.Server.Services.Twitch.PuntoSwitcher;

public interface IPuntoSwitcherService
{
    OperationResult<PuntoSwitchSuggestion> TryFixMessage(string? message);
}
