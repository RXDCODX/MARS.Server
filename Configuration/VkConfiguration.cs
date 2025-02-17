namespace MARS.Server.Configuration;

public class VkConfiguration
{
    public static string SectionName { get; set; } = "Vk";
    public long GroupId { get; set; }
    public required string UserKey { get; set; }
    public required string GroupKey { get; set; }
}
