using Microsoft.AspNetCore.Http;

namespace MARS.Server.Controllers;

public class MediaInfoUpsertRequest
{
    public required string AlertJson { get; set; }

    public IFormFile? File { get; set; }
}