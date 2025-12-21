using MARS.Server.Services.TekkenClans.Entities.Abstract;

namespace MARS.Server.Services.TekkenClans.Entities;

public class TekkenOldNickname : TekkenEntity
{
    public Guid? PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public TekkenPlayer? Player { get; set; }
}
