namespace MARS.Server.Services.BooruShared.Entities;

public abstract class BooruAutoPostUpdateRequestBase : BooruAutoPostCreateRequestBase
{
    public Guid Id { get; set; }
}
