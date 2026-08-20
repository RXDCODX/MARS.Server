using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostUpdateRequest : BooruAutoPostUpdateRequestBase
{
    public TargetPlatform TargetPlatform { get; set; }

    public string TelegramChannelId { get; set; } = "";

    public int PlanningHorizonDays { get; set; } = 60;

    public TelegramParseMode TelegramParseMode { get; set; }
}
