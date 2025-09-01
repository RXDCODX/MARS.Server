namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class Log
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }
    public DateTimeOffset WhenLogged { get; set; } = DateTimeOffset.Now;
    public required string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string LogLevel { get; set; } = string.Empty;
}
