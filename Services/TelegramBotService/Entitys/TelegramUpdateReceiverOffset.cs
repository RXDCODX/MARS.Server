namespace MARS.Server.Services.TelegramBotService.Entitys;

public class TelegramUpdateReceiverOffset
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public Guid Id { get; set; }

    public int Offset { get; set; }
}
