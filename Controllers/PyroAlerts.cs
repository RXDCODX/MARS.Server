using MARS.Server.Services.PyroAlerts;
using Microsoft.AspNetCore.Mvc;

namespace MARS.Server.Controllers;

[Route("memory")]
public class PyroAlerts : Controller
{
    // GET
    [HttpGet]
    [Route("{*escapedFileName:required}")]
    public async Task Index(string escapedFileName)
    {
        var context = ControllerContext;
        var fileName = Uri.UnescapeDataString(escapedFileName);
        var isFound = MemoryStorage.FileExists(fileName);

        if (isFound)
        {
            (MemoryStream stream, var contentType) =
                await MemoryStorage.GetFileStreamWithContentTypeAsync(fileName);
            var result = new FileStreamResult(stream, contentType)
            {
                EnableRangeProcessing = true,
                LastModified = DateTimeOffset.Now,
            };

            await result.ExecuteResultAsync(context);
            await MemoryStorage.DeleteFileAsync(fileName);
            return;
        }

        var result2 = new BadRequestResult();
        await result2.ExecuteResultAsync(context);
    }
}
