using ShikimoriSharp.Classes;

namespace MARS.Server.Services.WaifuRoll;

public class WaifuRollService(WaifuRollWorker worker) : ITelegramusService
{
    public Task<Waifu?> RollTheWaifu(
        string id,
        string? displayName = null,
        bool forcePass = false
    ) => worker.RollTheWaifu(id, displayName, forcePass);

    public Task<(Waifu? waifu, Host? host, Host? husband)> TelegramRollWaifu(string name) =>
        worker.TelegramRollWaifu(name);

    public Task<(Waifu?, bool)> AddNewWaifu(FullCharacter character) =>
        worker.AddNewWaifu(character);

    public Task<bool> MergeTheWaifu(Host host, Waifu waifu, bool makeprivate = true) =>
        worker.MergeTheWaifu(host, waifu, makeprivate);

    public Task<string?> AutoHello(string id, string displayName) =>
        worker.AutoHello(id, displayName);
}
