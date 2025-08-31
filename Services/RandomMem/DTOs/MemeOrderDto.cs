using System.ComponentModel.DataAnnotations;

namespace MARS.Server.Services.RandomMem.DTOs;

public class MemeOrderDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    
    [Required(ErrorMessage = "FilePath is required")]
    public string FilePath { get; set; } = string.Empty;
    
    public int? MemeTypeId { get; set; }
    public MemeTypeDto? Type { get; set; }
}

public class CreateMemeOrderDto
{
    [Required(ErrorMessage = "FilePath is required")]
    public string FilePath { get; set; } = string.Empty;
    
    public int? MemeTypeId { get; set; }
}

public class UpdateMemeOrderDto
{
    [Required(ErrorMessage = "FilePath is required")]
    public string FilePath { get; set; } = string.Empty;
    
    public int? MemeTypeId { get; set; }
    
    [Required(ErrorMessage = "Order is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Order must be greater than 0")]
    public int Order { get; set; }
}

public class MemeOrderWithTypeDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? MemeTypeId { get; set; }
    public string? TypeName { get; set; }
    public string? TypeFolderPath { get; set; }
}
