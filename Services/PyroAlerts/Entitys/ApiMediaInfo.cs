using System.Diagnostics.CodeAnalysis;

namespace MARS.Server.Services.PyroAlerts.Entitys;

/// <summary>
/// API-обёртка над MediaInfo для ответов контроллера
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public class ApiMediaInfo : MediaInfo
{
    public ApiMediaInfo() { }

    [SetsRequiredMembers]
    public ApiMediaInfo(ApiMediaInfo source)
        : base(source) { }

    [SetsRequiredMembers]
    public ApiMediaInfo(MediaInfo source)
        : base(source) { }
}
