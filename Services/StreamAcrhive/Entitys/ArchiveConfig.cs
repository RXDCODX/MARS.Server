namespace MARS.Server.Services.StreamAcrhive.Entitys;

public class StreamArchiveConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public ulong TelegramChannelId { get; set; }
    public string FileNameFormat { get; set; } = null!;
    public TimeSpan CheckSpan { get; set; }
    public string FolderPath { get; set; } = null!;
    public bool IsConvertFile { get; set; }
    public StreamArchiveVideoFormats FileConvertType { get; set; }
}
