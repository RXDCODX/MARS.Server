using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.DanbooruAutoPost.Entities;

public class DanbooruAutoPostCreateRequest : BooruAutoPostCreateRequestBase
{
    public TargetPlatform TargetPlatform { get; set; }

    public string TelegramChannelId { get; set; } = "";

    public int PlanningHorizonDays { get; set; } = 60;
}
