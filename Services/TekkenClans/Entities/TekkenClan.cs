using MARS.Server.Services.TekkenClans.Entities.Abstract;

namespace MARS.Server.Services.TekkenClans.Entities;

public class TekkenClan : TekkenEntity
{
    public string? ShortName
    {
        get;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (value is { Length: >= 2 and <= 5 })
                {
                    field = value;
                }
            }

            throw new FormatException("Wrong Clan shortname");
        }
    }

    public Guid? TekkenHeadBannerId { get; set; }

    [ForeignKey(nameof(TekkenHeadBannerId))]
    public TekkenClanHeadBanner? HeadBanner { get; set; }

    public Guid? TekkenBodyBannerId { get; set; }

    [ForeignKey(nameof(TekkenBodyBannerId))]
    public TekkenBodyBanner? BodyBanner { get; set; }

    public Guid? TekkenIconId { get; set; }

    [ForeignKey(nameof(TekkenIconId))]
    public TekkenIcon? TekkenIcon { get; set; }
}
