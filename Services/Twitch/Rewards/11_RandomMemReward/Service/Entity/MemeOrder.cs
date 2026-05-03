namespace MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;

public class MemeOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Order { get; set; }

    [MaxLength(int.MaxValue)]
    public required string FilePath { get; set; }
    public int? MemeTypeId { get; set; }
    public MemeType? Type { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is MemeOrder newOrder && Equals(newOrder);
    }

    protected bool Equals(MemeOrder other)
    {
        return Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
}
