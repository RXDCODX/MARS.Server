namespace MARS.Server.Services.PyroAlerts.Entitys;

public enum MediaType
{
    None,
    Image, //Изображение
    Audio, //Аудио, музыка, голосовое сообщение
    Video, //Видео, может быть разных фарматов, наиболее вероятные mp4/webm
    TelegramSticker, //tgs стикеры телеги
    Voice,
    Gif,
}
