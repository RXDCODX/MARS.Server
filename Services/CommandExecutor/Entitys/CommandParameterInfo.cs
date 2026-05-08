namespace MARS.Server.Services.CommandExecutor.Entitys;

public class CommandParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
    public string? DefaultValue { get; set; }
}
