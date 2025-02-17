namespace MARS.Server.DataBaseContext;

public class Log
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }
    public DateTimeOffset WhenLogged { get; set; } = DateTimeOffset.Now;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
