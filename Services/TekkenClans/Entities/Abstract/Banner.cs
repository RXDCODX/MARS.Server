namespace MARS.Server.Services.TekkenClans.Entities.Abstract;

public interface IBanner
{
    public byte[]? FileContent { get; set; }

    public string? Name { get; set; }
}
