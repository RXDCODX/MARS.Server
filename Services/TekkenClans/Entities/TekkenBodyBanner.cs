using MARS.Server.Services.TekkenClans.Entities.Abstract;

namespace MARS.Server.Services.TekkenClans.Entities;

public class TekkenBanner : TekkenEntity, IBanner
{
    public Guid? TekkenClanId { get; set; }

    [ForeignKey(nameof(TekkenClanId))]
    public TekkenClan? TekkenClan { get; set; }

    public byte[]? FileContent { get; set; }
}
