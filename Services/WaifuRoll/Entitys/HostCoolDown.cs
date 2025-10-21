namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("CD")]
public class HostCoolDown
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Guid { get; set; } = Guid.NewGuid();

    public required string HostId { get; set; }

    [ForeignKey(nameof(HostId))]
    public Host? Host { get; set; }
    public DateTimeOffset Time { get; set; }
}
