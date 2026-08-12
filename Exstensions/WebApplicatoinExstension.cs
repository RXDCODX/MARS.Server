using System.Text;
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

            return string.IsNullOrEmpty(cleanPath)
                ? new NotFoundFileInfo(subpath)
                : base.GetFileInfo(cleanPath);
        }

        public new IDirectoryContents GetDirectoryContents(string subpath)
        {
            var cleanPath = subpath;
            if (subpath.StartsWith("/static", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = subpath.Substring("/static".Length);
            }

            return base.GetDirectoryContents(
                string.IsNullOrEmpty(cleanPath) ? string.Empty : cleanPath
            );
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

    public static WebApplicationBuilder AddStaticFilesBrowserOptions(this WebApplicationBuilder app)
    {
        var sharedOptions = new SharedOptions
        {
            RequestPath = "/static",
            RedirectToAppendTrailingSlash = true,
            FileProvider = new StaticAssetsFileProvider(app.Environment.WebRootPath),
        };

        app.Services.AddSingleton(sharedOptions);

        return app;
    }

    public static IApplicationBuilder AddStaticFilesBrowser(this WebApplication app)
    {
        var sharedOptions = app.Services.GetRequiredService<SharedOptions>();

        var fileOptions = new StaticFileOptions
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

    public static IApplicationBuilder AddDiSchemaRoute(
        this WebApplication app,
        IServiceCollection services
    )
    {
        app.Map(
            "/allservices",
            builder =>
                builder.Run(async context =>
                {
                    var sb = new StringBuilder();
                    var dependencies = new Dictionary<string, string>();
                    sb.AppendLine("<pre>");
                    sb.AppendLine("digraph Services {");
                    var servicesDi = services.Select(svc => svc.ServiceType.ToString()).ToHashSet();

                    foreach (var svc in services)
                    {
                        var implementationName = svc.ImplementationType?.ToString();
                        if (implementationName != null)
                        {
                            var implDependencies = svc
                                .ImplementationType?.GetConstructors()
                                .SelectMany(cons => cons.GetParameters())
                                .Select(p => p.ParameterType.ToString())
                                .Distinct()
                                .Where(servicesDi.Contains)
                                .ToList();

                            if (implDependencies is { Count: > 0 })
                            {
                                // Register Constructor dependendencies
                                foreach (var d in implDependencies)
                                {
                                    dependencies.TryAdd(implementationName, d);
                                }
                            }
                        }
                    }

                    void PrintGroup(
                        string label,
                        string cluster,
                        string color,
                        IEnumerable<string> group
                    )
                    {
                        if (group.Count() > 0)
                        {
                            sb.AppendLine($"  subgraph cluster_{cluster} {{");
                            sb.AppendLine("      style = filled;");
                            sb.AppendLine($"     color = {color};");
                            sb.AppendLine("      node[style = filled, color = white];");
                            sb.AppendLine($"     label = \"{label}\";");
                            foreach (var item in group)
                            {
                                sb.AppendLine($"     \"{item}\"");
                            }

                            sb.AppendLine("  }");
                        }
                    }

                    var scopedGroup = services
                        .Where(s => s.Lifetime == ServiceLifetime.Scoped)
                        .Select(s => s.ServiceType.ToString());
                    var transientGroup = services
                        .Where(s => s.Lifetime == ServiceLifetime.Transient)
                        .Select(s => s.ServiceType.ToString());
                    PrintGroup("scoped", "0", "blue", scopedGroup);
                    PrintGroup("transient", "1", "lightgrey", transientGroup);
                    // Make interfaces different
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("     node [color=green]");
                    var interfacesGroup = services
                        .Where(s => s.ServiceType.IsInterface)
                        .Select(s => s.ServiceType.ToString());
                    foreach (var @interface in interfacesGroup)
                    {
                        sb.AppendLine($"     \"{@interface}\"");
                    }
                    sb.AppendLine();
                    sb.AppendLine("    node [color=black]");
                    var noninterfacesGroup = services
                        .Where(s => !s.ServiceType.IsInterface)
                        .Select(s => s.ServiceType.ToString());
                    foreach (var nonInterface in noninterfacesGroup)
                    {
                        sb.AppendLine($"     \"{nonInterface}\"");
                    }
                    //Now print dependencies
                    foreach (var d in dependencies)
                    {
                        sb.AppendLine($"     \"{d.Key}\" -> \"{d.Value}\"");
                    }
                    sb.AppendLine("}");
                    sb.AppendLine("</pre>");
                    // Ok ready. Now return all the graph
                    await context.Response.WriteAsync(sb.ToString());
                })
        );

        return app;
    }
}
