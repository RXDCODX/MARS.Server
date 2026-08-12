using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.Services.Twitch.ClientMessages.TwitchAutoHello.Entitys;

public class AutoVideoHello
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string TwitchId { get; set; }
    public DateTime LastPostDateTime { get; set; }
    public required byte[] File { get; set; }
    public required string FileExtension { get; set; }
}
