using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MARS.Server.CustomLoggers.DatabaseLogger;

public class Log
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid Id { get; set; }
    public DateTime WhenLogged { get; set; } = DateTime.Now;
    public required string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public LogLevel LogLevel { get; set; }
}
