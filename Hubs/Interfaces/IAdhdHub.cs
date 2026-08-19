using MARS.Server.Services.Adhd.Entities;

namespace MARS.Server.Hubs.Interfaces;

public interface IAdhdHub
{
    Task ReceiveConfig(AdhdLayoutConfigDto config);
    Task ConfigUpdated(AdhdLayoutConfigDto config);
}
