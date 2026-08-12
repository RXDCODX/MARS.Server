namespace MARS.Server.Services.PyroAlerts.Entitys;

public class MediaPositionInfo
{
    public bool IsProportion { get; set; } = true;
    public bool IsResizeRequires { get; set; } = false;
    public int Height { get; set; } = 500;
    public int Width { get; set; } = 500;
    public bool IsRotated { get; set; } = true;
    public int Rotation { get; set; } = Random.Shared.Next(0, 41);
    public int XCoordinate { get; set; } = 0; //X координата для отображения на странице
    public int YCoordinate { get; set; } = 0; //Y координата для отображения на странице
    public bool RandomCoordinates { get; set; } = true; //Указатель что координаты для расположения на странице файла должны быть выбраны случайным образом

    public bool IsVerticallCenter { get; set; } = false;
    public bool IsHorizontalCenter { get; set; } = false;
    public bool IsUseOriginalWidthAndHeight { get; set; } = true;
}
