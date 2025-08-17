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
            // Убираем "/static" из пути, если он присутствует
            var cleanPath = subpath;
            if (subpath.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = subpath.Substring("/static".Length);
            }

            // Если путь пустой, возвращаем null
            if (string.IsNullOrEmpty(cleanPath))
            {
                return new NotFoundFileInfo(subpath);
            }

            // Вызываем базовый метод с очищенным путем
            return base.GetFileInfo(cleanPath);
        }

        public new IDirectoryContents GetDirectoryContents(string subpath)
        {
            // Убираем "/static" из пути, если он присутствует
            var cleanPath = subpath;
            if (subpath.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = subpath.Substring("/static".Length);
            }

            // Если путь пустой, возвращаем содержимое корневой директории
            if (string.IsNullOrEmpty(cleanPath))
            {
                return base.GetDirectoryContents(string.Empty);
            }

            // Вызываем базовый метод с очищенным путем
            return base.GetDirectoryContents(cleanPath);
        }

        public new IChangeToken Watch(string filter)
        {
            // Убираем "/static" из фильтра, если он присутствует
            var cleanFilter = filter;
            if (filter.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanFilter = filter.Substring("/static".Length);
            }

            // Вызываем базовый метод с очищенным фильтром
            return base.Watch(cleanFilter);
        }

        public StaticAssetsFileProvider(string root)
            : base(root) { }

        public StaticAssetsFileProvider(string root, ExclusionFilters filters)
            : base(root, filters) { }
    }

    public static WebApplicationBuilder AddStaticFilesBrowserOptions(this WebApplicationBuilder app)
    {
        var sharedOptions = new SharedOptions()
        {
            RequestPath = "/static",
            RedirectToAppendTrailingSlash = true,
            FileProvider = new StaticAssetsFileProvider(app.Environment.WebRootPath),
        };
        //if (env.IsProduction())
        //{
        //    sharedOptions.FileProvider = new StaticAssetsFileProvider(
        //        Path.Combine(directory, "wwwroot")
        //    );
        //}

        app.Services.AddSingleton(sharedOptions);

        return app;
    }

    public static IApplicationBuilder AddStaticFilesBrowser(this WebApplication app)
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
