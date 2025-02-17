namespace MARS.Server.Services.Twitch.ClientMessages.AutoMessages.Entitys;

public class AutoMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public required string Message { get; set; }
}
