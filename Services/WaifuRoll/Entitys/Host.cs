namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("Hosts")]
public class Host
{
    public string? Name { get; set; }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }
    public DateTimeOffset WhenOrdered { get; set; }
    public string? WaifuBrideId { get; set; }
    public bool IsPrivated { get; set; }
    public long OrderCount { get; set; }
    public string? WaifuRollId { get; set; }
    public DateTimeOffset? WhenPrivated { get; set; }
    public required HostAutoHello HostGreetings { get; set; }
    public required HostCoolDown HostCoolDown { get; set; }
}
