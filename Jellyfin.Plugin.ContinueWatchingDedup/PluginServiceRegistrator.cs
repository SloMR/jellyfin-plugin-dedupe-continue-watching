using Jellyfin.Plugin.ContinueWatchingDedup.Middleware;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jellyfin.Plugin.ContinueWatchingDedup;

/// <summary>
/// Registers the deduplication middleware into the ASP.NET Core pipeline
/// when Jellyfin starts up.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Transient<IStartupFilter, DedupStartupFilter>());
    }
}

/// <summary>
/// Inserts the dedup middleware near the start of the request pipeline
/// so it runs before Jellyfin's own controllers respond.
/// </summary>
public class DedupStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var middleware = new DedupMiddleware(
                    _ => nextMiddleware(),
                    app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DedupMiddleware>>());
                await middleware.InvokeAsync(context);
            });
            next(app);
        };
    }
}
