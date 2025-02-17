namespace MARS.Server.Configuration;

#pragma warning disable CA1050 // Declare types in namespaces
#pragma warning disable RCS1110 // Declare type inside namespace.
public class ChannelsSpy
{
    public static readonly string Configuration = "ChannelsSpy";

    public IEnumerable<string>? Channels { get; set; }
}
