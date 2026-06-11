using System.Threading.Tasks;

namespace MARS.Server.Services.SoundBarService.Entitys;

public interface ISoundBar
{
    public Task Mute(params string[] args);
    public Task Unmute();
    public Task<string> GetBagCount();
}
