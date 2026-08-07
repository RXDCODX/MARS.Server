using SevenTV.Types.Rest;

namespace MARS.Server.Services.SevenTv;

public interface ISevenTvApiService
{
    Task<User?> GetUserAsync(string userId);
    Task<EmoteSet?> GetEmoteSetAsync(string emoteSetId);
}
