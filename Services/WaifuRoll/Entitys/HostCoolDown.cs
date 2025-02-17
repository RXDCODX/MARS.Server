namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("CD")]
public class HostCoolDown
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Guid { get; set; } = Guid.NewGuid();

    [ForeignKey("HostId")]
    public required string HostId { get; set; }

    public Host? Host { get; set; }
    public DateTimeOffset Time { get; set; }
}
