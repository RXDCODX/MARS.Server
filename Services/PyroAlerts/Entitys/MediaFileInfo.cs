namespace MARS.Server.Services.PyroAlerts.Entitys;

public class MediaFileInfo
{
    public required MediaType Type { get; set; } //Тип файла который указан в FilePath,
    public required string FilePath { get; set; }
    public bool IsLocalFile { get; set; } = true;
    public required string FileName { get; set; }
    public required string Extension { get; set; }
    public bool IsFileNotConvertable { get; set; } = false;
}
