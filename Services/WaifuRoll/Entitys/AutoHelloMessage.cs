using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table("AutoHelloMessages")]
public class AutoHelloMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Guid { get; set; } = Guid.NewGuid();

    [MaxLength(500)]
    public required string Text { get; set; }

    public int Order { get; set; }
}
