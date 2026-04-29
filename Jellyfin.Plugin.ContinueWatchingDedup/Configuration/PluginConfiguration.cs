using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ContinueWatchingDedup.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// When true, only the most recently played episode of each series
    /// appears in /Items/Resume. When false, the plugin acts as a no-op.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, movies are also deduplicated (only relevant if you have
    /// multiple versions of the same movie). Defaults to false because
    /// movies rarely benefit from this and it adds work.
    /// </summary>
    public bool DeduplicateMovies { get; set; } = false;

    /// <summary>
    /// Maximum episodes per series to keep. 1 = strict (only latest),
    /// 2+ = keep N most recently played. Default 1 matches the
    /// "show me my current episode" use case.
    /// </summary>
    public int MaxEpisodesPerSeries { get; set; } = 1;
}
