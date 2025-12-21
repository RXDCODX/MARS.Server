using MARS.Server.Services.TekkenClans.Entities.Abstract;

namespace MARS.Server.Services.TekkenClans.Entities;

public class TekkenPlayer : TekkenEntity
{
    public TekkenId? TekkenId { get; set; }

    public Guid? ClanId { get; set; }

    [ForeignKey(nameof(ClanId))]
    public TekkenClan? Clan { get; set; }
}
