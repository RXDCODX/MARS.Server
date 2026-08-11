using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.WaifuRoll.Entitys;

[Table(nameof(WaifuRollAudio) + "s")]
public class WaifuRollAudio
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [Required]
    public required byte[] AudioData { get; set; }

    [Required]
    [MaxLength(20)]
    public required string FileExtension { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
