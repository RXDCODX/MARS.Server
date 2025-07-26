namespace MARS.Server.Services.ServiceManager.Entitys;

public class ServiceBaseConfigurationInfo
{
    public Guid Id { get; set; }
    public required string ServiceName { get; set; }
    public required string ServiceDescription { get; set; }
}
