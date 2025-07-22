using ShikimoriSharp.Classes;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService(WaifuRollWorker worker) : ITelegramusService
{
    private readonly WaifuRollWorker _worker = worker;

    public Task<Waifu?> RollTheWaifu(
        string id,
        string? displayName = null,
        bool forcePass = false
    ) => _worker.RollTheWaifu(id, displayName, forcePass);

    public Task<(Waifu? waifu, Host? host, Host? husband)> TelegramRollWaifu(string name) =>
        _worker.TelegramRollWaifu(name);

    public Task<(Waifu?, bool)> AddNewWaifu(FullCharacter character) =>
        _worker.AddNewWaifu(character);

    public Task<bool> MergeTheWaifu(Host host, Waifu waifu, bool makeprivate = true) =>
        _worker.MergeTheWaifu(host, waifu, makeprivate);

    public Task<string?> AutoHello(string id, string displayName) =>
        _worker.AutoHello(id, displayName);
}
