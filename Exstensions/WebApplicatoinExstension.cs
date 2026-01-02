using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace MARS.Server.Exstensions;

public static class WebApplicatoinExstension
{
    private class StaticAssetsFileProvider : PhysicalFileProvider, IFileProvider
    {
        public new IFileInfo GetFileInfo(string subpath)
        {
            var cleanPath = subpath;
            if (subpath.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = subpath.Substring("/static".Length);
            }

            return string.IsNullOrEmpty(cleanPath) ? new NotFoundFileInfo(subpath) : base.GetFileInfo(cleanPath);
        }

        public new IDirectoryContents GetDirectoryContents(string subpath)
        {
            var cleanPath = subpath;
            if (subpath.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = subpath.Substring("/static".Length);
            }

            return base.GetDirectoryContents(string.IsNullOrEmpty(cleanPath) ? string.Empty : cleanPath);
        }

        public new IChangeToken Watch(string filter)
        {
            var cleanFilter = filter;
            if (filter.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanFilter = filter.Substring("/static".Length);
            }

            return base.Watch(cleanFilter);
        }

        public StaticAssetsFileProvider(string root)
            : base(root) { }

        public StaticAssetsFileProvider(string root, ExclusionFilters filters)
            : base(root, filters) { }
    }

    extension(WebApplicationBuilder app)
    {
        public WebApplicationBuilder AddStaticFilesBrowserOptions()
        {
            var sharedOptions = new SharedOptions()
            {
                RequestPath = "/static",
                RedirectToAppendTrailingSlash = true,
                FileProvider = new StaticAssetsFileProvider(app.Environment.WebRootPath),
            };

            app.Services.AddSingleton(sharedOptions);

            return app;
        }
    }

    extension(WebApplication app)
    {
        public IApplicationBuilder AddStaticFilesBrowser()
        {
            var sharedOptions = app.Services.GetRequiredService<SharedOptions>();

            var fileOptions = new StaticFileOptions()
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
                FileProvider = sharedOptions.FileProvider,
                RedirectToAppendTrailingSlash = true,
            };

            app.UseDirectoryBrowser(new DirectoryBrowserOptions(sharedOptions) { });

            app.UseStaticFiles(fileOptions);
            return app;
        }
    }
}
