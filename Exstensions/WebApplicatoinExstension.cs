using System.IO;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;

namespace MARS.Server.Exstensions;

public static class WebApplicatoinExstension
{
    public static IApplicationBuilder AddStaticFilesBrowser(
        this WebApplication app,
        string directory
    )
    {
        var env = app.Environment;
        var sharedOptions = new SharedOptions();
        if (env.IsProduction())
        {
            sharedOptions.FileProvider = new PhysicalFileProvider(
                Path.Combine(directory, "wwwroot")
            );
        }

        var fileOptions = new StaticFileOptions(sharedOptions)
        {
            ServeUnknownFileTypes = true,
            OnPrepareResponse = context =>
            {
                var path = context.File.PhysicalPath;
                var exst = Path.GetExtension(path);
                if (exst == ".tgs")
                {
                    context.Context.Response.ContentType = string.Empty;
                }
            },
            RedirectToAppendTrailingSlash = true,
            RequestPath = PathString.FromUriComponent("/staticfiles"),
        };

        app.UseDirectoryBrowser(
            new DirectoryBrowserOptions(sharedOptions) { RedirectToAppendTrailingSlash = true }
        );

        app.UseStaticFiles(fileOptions);
        return app;
    }
}
