namespace MARS.Server.Services.RandomMem.Entity;

public class VideoOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Order { get; set; }
    public required string FilePath { get; set; }
}
