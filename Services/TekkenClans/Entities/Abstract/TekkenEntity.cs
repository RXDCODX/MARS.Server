namespace MARS.Server.Services.TekkenClans.Entities.Abstract;

public abstract class TekkenEntity
{
    [Key]
    public Guid Id { get; set; }
    public string? Name { get; set; }
}
