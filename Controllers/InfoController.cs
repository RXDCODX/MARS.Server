using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace MARS.Server.Controllers;

[Route("/-/{controller}")]
public class InfoController(IEnumerable<EndpointDataSource> endpointSources) : Controller
{
    [HttpGet("endpoints")]
    public ActionResult ListAllEndpoints()
    {
        var endpoints = endpointSources.SelectMany(es => es.Endpoints).OfType<RouteEndpoint>();
        var output = endpoints.Select(e =>
        {
            var controller = e.Metadata.OfType<ControllerActionDescriptor>().FirstOrDefault();
            var action =
                controller != null ? $"{controller.ControllerName}.{controller.ActionName}" : null;
            var controllerMethod =
                controller != null
                    ? $"{controller.ControllerTypeInfo.FullName}:{controller.MethodInfo.Name}"
                    : null;
            return new
            {
                Method = e.Metadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods?[0],
                Route = $"/{e.RoutePattern.RawText?.TrimStart('/')}",
                Action = action,
                ControllerMethod = controllerMethod,
            };
        });

        return Json(output);
    }
}
