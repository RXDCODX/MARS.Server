using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;

namespace MARS.Server.Exstensions;

public static class WebApplicatoinExstension
{
    public static WebApplicationBuilder AddStaticFilesBrowserOptions(
        this WebApplicationBuilder app,
        string directory
    )
    {
        var env = app.Environment;
        var sharedOptions = new SharedOptions()
        {
            RequestPath = "/static",
            RedirectToAppendTrailingSlash = true,
        };
        if (env.IsProduction())
        {
            sharedOptions.FileProvider = new PhysicalFileProvider(
                Path.Combine(directory, "wwwroot")
            );
        }

        app.Services.AddSingleton(sharedOptions);

        return app;
    }

    public static IApplicationBuilder AddStaticFilesBrowser(this WebApplication app)
    {
        var sharedOptions = app.Services.GetRequiredService<SharedOptions>();

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
        };

        app.UseDirectoryBrowser(new DirectoryBrowserOptions(sharedOptions) { });

        app.UseStaticFiles(fileOptions);
        return app;
    }
}
