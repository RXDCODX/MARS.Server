namespace MARS.Server.Services.Twitch.Rewards._11_RandomMemReward.Service.Entity;

public class MemeType
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(int.MaxValue)]
    public required string FolderPath { get; set; }
}
