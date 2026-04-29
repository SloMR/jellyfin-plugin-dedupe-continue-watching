using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ContinueWatchingDedup.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ContinueWatchingDedup;

/// <summary>
/// Plugin entry point. Deduplicates the Continue Watching row so each series
/// appears only once — represented by the most recently played episode.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Continue Watching Deduplicator";

    public override Guid Id => Guid.Parse("c1a8f9b4-7e3d-4c2a-9f8e-3d4a5b6c7d8e");

    public override string Description =>
        "Removes duplicate episodes from the Continue Watching row, " +
        "showing only the most recently played episode per series.";

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        };
    }
}
