using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table(nameof(HusbandAutoHello) + "Cooldowns")]
public class HusbandAutoHello
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Guid { get; set; } = Guid.NewGuid();

    public required string HusbandId { get; set; }

    [ForeignKey(nameof(HusbandId))]
    public Husband? Husband { get; set; }

    public DateTime Time { get; set; }
}
