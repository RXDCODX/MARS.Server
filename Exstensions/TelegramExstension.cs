namespace MARS.Server.Exstensions;

public static class TelegramExstension
{
    public const long Rxdcodx = 402763435;

    public const long RxdcodxChannel = -1001389649351;
    public const string RxdcodxChannelName = "rxdcodx";
    public const long TelegramMemesPublic = -1002111311866;
    public const long PornPublic = -1002372599687;

    public static InputFileStream FromByteArray(byte[] arrayBytes, string? fileName)
    {
        var stream = new MemoryStream(arrayBytes);
        return InputFile.FromStream(stream, fileName);
    }
}
