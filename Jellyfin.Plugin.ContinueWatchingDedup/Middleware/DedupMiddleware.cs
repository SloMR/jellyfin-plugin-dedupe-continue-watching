using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContinueWatchingDedup.Middleware;

/// <summary>
/// Intercepts responses to /Users/{userId}/Items/Resume and removes duplicate
/// episodes belonging to the same series, keeping only the most recently played.
/// </summary>
public class DedupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DedupMiddleware> _logger;

    public DedupMiddleware(RequestDelegate next, ILogger<DedupMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!IsResumeEndpoint(path))
        {
            await _next(context);
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.Enabled)
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            if (context.Response.StatusCode != 200)
            {
                buffer.Seek(0, SeekOrigin.Begin);
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
                return;
            }

            buffer.Seek(0, SeekOrigin.Begin);
            var rawBytes = buffer.ToArray();
            if (rawBytes.Length == 0)
            {
                context.Response.Body = originalBody;
                return;
            }

            // Detect and decompress based on Content-Encoding
            var encoding = context.Response.Headers.ContentEncoding.ToString();
            string json;
            try
            {
                json = await DecompressAsync(rawBytes, encoding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CWDedup] Failed to decompress response (encoding={Encoding})", encoding);
                context.Response.Body = originalBody;
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            if (string.IsNullOrEmpty(json))
            {
                context.Response.Body = originalBody;
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            string modified;
            try
            {
                modified = Deduplicate(json, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CWDedup] Dedup failed for {Path}", path);
                context.Response.Body = originalBody;
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            // Recompress if the original was compressed
            var newBytes = Encoding.UTF8.GetBytes(modified);
            if (!string.IsNullOrEmpty(encoding))
            {
                newBytes = await CompressAsync(newBytes, encoding);
            }

            context.Response.Body = originalBody;
            context.Response.ContentLength = newBytes.Length;
            await context.Response.Body.WriteAsync(newBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CWDedup] Middleware error");
            context.Response.Body = originalBody;
        }
    }

    private static async Task<string> DecompressAsync(byte[] data, string encoding)
    {
        if (string.IsNullOrEmpty(encoding))
        {
            return Encoding.UTF8.GetString(data);
        }

        using var input = new MemoryStream(data);
        Stream decompressionStream = encoding.ToLowerInvariant() switch
        {
            "gzip" => new GZipStream(input, CompressionMode.Decompress),
            "br" => new BrotliStream(input, CompressionMode.Decompress),
            "deflate" => new DeflateStream(input, CompressionMode.Decompress),
            _ => input  // unknown encoding, return as-is
        };

        using (decompressionStream)
        using (var reader = new StreamReader(decompressionStream, Encoding.UTF8))
        {
            return await reader.ReadToEndAsync();
        }
    }

    private static async Task<byte[]> CompressAsync(byte[] data, string encoding)
    {
        using var output = new MemoryStream();
        Stream compressionStream = encoding.ToLowerInvariant() switch
        {
            "gzip" => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "br" => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
            "deflate" => new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true),
            _ => null!
        };

        if (compressionStream is null) return data;

        using (compressionStream)
        {
            await compressionStream.WriteAsync(data);
        }

        return output.ToArray();
    }

    private static bool IsResumeEndpoint(string path)
    {
        var trimmed = path.Trim('/');
        var parts = trimmed.Split('/');

        // Pattern 1: /Users/{userId}/Items/Resume (Jellyfin Web, official Android)
        if (parts.Length >= 4
            && string.Equals(parts[0], "Users", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[2], "Items", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[3], "Resume", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Pattern 2: /UserItems/Resume (SwiftFin iOS, Wholphin Android - SDK-style)
        if (parts.Length == 2
            && string.Equals(parts[0], "UserItems", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "Resume", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Pattern 3: /Shows/Resume (some clients)
        if (parts.Length == 2
            && string.Equals(parts[0], "Shows", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "Resume", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses the response JSON, deduplicates Items by SeriesId,
    /// keeps the most recently played per series, and re-serializes.
    /// </summary>
    private string Deduplicate(string json, Configuration.PluginConfiguration config)
    {
        var root = JsonNode.Parse(json);
        if (root is null) return json;

        var itemsNode = root["Items"]?.AsArray();
        if (itemsNode is null || itemsNode.Count < 2) return json;

        // Group items by series (or by item ID if movie/no series)
        var groups = new Dictionary<string, List<(JsonNode node, DateTime lastPlayed)>>();
        var passthrough = new List<JsonNode>();

        foreach (var item in itemsNode)
        {
            if (item is null) continue;

            var itemType = item["Type"]?.GetValue<string>() ?? string.Empty;
            var seriesId = item["SeriesId"]?.GetValue<string>();
            var lastPlayed = ParseDate(item["UserData"]?["LastPlayedDate"]?.GetValue<string>());

            // Episodes always group by SeriesId
            if (string.Equals(itemType, "Episode", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(seriesId))
            {
                if (!groups.TryGetValue(seriesId, out var list))
                {
                    list = new List<(JsonNode, DateTime)>();
                    groups[seriesId] = list;
                }
                list.Add((item, lastPlayed));
                continue;
            }

            // Movies — only deduplicate if explicitly enabled
            if (config.DeduplicateMovies && string.Equals(itemType, "Movie", StringComparison.OrdinalIgnoreCase))
            {
                var movieKey = $"movie:{item["Id"]?.GetValue<string>()}";
                if (!groups.TryGetValue(movieKey, out var list))
                {
                    list = new List<(JsonNode, DateTime)>();
                    groups[movieKey] = list;
                }
                list.Add((item, lastPlayed));
                continue;
            }

            // Anything else passes through unchanged
            passthrough.Add(item);
        }

        // For each group, keep the top N items by LastPlayedDate
        var keep = new List<JsonNode>();
        keep.AddRange(passthrough);

        var maxPerSeries = Math.Max(1, config.MaxEpisodesPerSeries);
        foreach (var entry in groups.Values)
        {
            var ordered = entry
                .OrderByDescending(t => t.lastPlayed)
                .Take(maxPerSeries)
                .Select(t => t.node);
            keep.AddRange(ordered);
        }

        // Preserve original ordering (by index in the input)
        var indexMap = new Dictionary<JsonNode, int>();
        for (int i = 0; i < itemsNode.Count; i++)
        {
            if (itemsNode[i] is JsonNode n) indexMap[n] = i;
        }
        keep.Sort((a, b) =>
            (indexMap.TryGetValue(a, out var ia) ? ia : int.MaxValue)
            .CompareTo(indexMap.TryGetValue(b, out var ib) ? ib : int.MaxValue));

        var newArray = new JsonArray();
        foreach (var node in keep)
        {
            // Detach from parent before re-adding
            var clone = JsonNode.Parse(node.ToJsonString());
            if (clone is not null) newArray.Add(clone);
        }

        root["Items"] = newArray;
        if (root["TotalRecordCount"] is not null) root["TotalRecordCount"] = newArray.Count;

        var hidden = itemsNode.Count - newArray.Count;
        if (hidden > 0)
        {
            _logger.LogDebug("Deduplicated Resume response: {Original} → {Final} ({Hidden} hidden)",
                itemsNode.Count, newArray.Count, hidden);
        }

        return root.ToJsonString();
    }

    private static DateTime ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTime.MinValue;
        return DateTime.TryParse(value, out var dt) ? dt : DateTime.MinValue;
    }
}
