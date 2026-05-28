using System.Collections.Generic;

namespace MARS.Server.Configuration;

public class ChannelsSpy
{
    public static readonly string Configuration = "ChannelsSpy";

    public IEnumerable<string>? Channels { get; set; }
}
