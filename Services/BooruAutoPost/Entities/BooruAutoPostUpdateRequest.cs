using MARS.Server.Services.BooruShared.Entities;

namespace MARS.Server.Services.BooruAutoPost.Entities;

public class BooruAutoPostUpdateRequest : BooruAutoPostUpdateRequestBase
{
    public BooruSource Source { get; set; }

    public TargetPlatform TargetPlatform { get; set; }

    public string TelegramChannelId { get; set; } = "";

    public int PlanningHorizonDays { get; set; } = 60;

    public int TargetPostCount { get; set; } = 1;

    public int? SpecificPostId { get; set; }

    public TelegramParseMode TelegramParseMode { get; set; }
}
