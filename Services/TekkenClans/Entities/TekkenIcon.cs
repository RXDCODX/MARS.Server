using MARS.Server.Services.TekkenClans.Entities.Abstract;

namespace MARS.Server.Services.TekkenClans.Entities;

public class TekkenIcon : TekkenEntity, IBanner
{
    public byte[]? FileContent { get; set; }
}
