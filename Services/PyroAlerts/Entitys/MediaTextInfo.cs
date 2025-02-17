namespace MARS.Server.Services.PyroAlerts.Entitys;

public class MediaTextInfo
{
    public string? KeyWordsColor { get; set; } //Массив из трех hex чисел для hex color ключевых слов в тексте, ключевые слова в тексте указываются фигурными скобками
    public string? TriggerWord { get; set; }
    public string? Text { get; set; } //Текст присылаемый с файлом
    public string? TextColor { get; set; } //Массив из трех hex чисел для hex color текста
    public char? KeyWordSybmolDelimiter { get; set; } = '#';
}
