using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace MARS.Server.Hubs;

[SignalRHub("/hubs/logger", AutoDiscover.MethodsAndParams)]
public class LoggerHub() : Hub<ILoggerHub>;
