using System.Collections.Concurrent;
using System.Xml.Linq;
using SolutionMapper.UI;

namespace SolutionMapper.Mapping;

public static class ProjectMetadataReader
{
    // ponytail: a .csproj is parsed once per process; the pipeline reads each file
    // several times (mapper M×N groups, dependency graph). Keyed by full path.
    // The cache holds the in-flight Task so concurrent callers await the same parse.
    static readonly ConcurrentDictionary<string, Task<ProjectMetadata?>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cached read. Same result as <see cref="TryReadAsync"/>, parsed at most once per path.</summary>
    public static Task<ProjectMetadata?> ReadAsync(string projectFile, CancellationToken ct = default)
    {
        var key = Path.GetFullPath(projectFile);
        if (Cache.TryGetValue(key, out var hit))
        {
            Metrics.Count("TryRead cache hit");
            return hit;
        }
        return Cache.GetOrAdd(key, k => TryReadAsync(k, ct));
    }

    public static async Task<ProjectMetadata?> TryReadAsync(string projectFile, CancellationToken ct = default)
    {
        using var _ = Metrics.Measure("ProjectMetadataReader.TryRead (XDocument.LoadAsync + walk)");
        try
        {
            await using var stream = new FileStream(
                projectFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            // ponytail: ignore MSBuild conditions/namespaces; static props only
            string? Prop(string name) =>
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();

            var assembly = Prop("AssemblyName");
            if (assembly is not null && assembly.Contains("$(")) assembly = null;

            var tfm = Prop("TargetFramework") ?? Prop("TargetFrameworks");
            var rootNs = Prop("RootNamespace");
            if (rootNs is not null && rootNs.Contains("$(")) rootNs = null;

            var refIncludes = doc.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            var refNames = refIncludes
                .Select(v => Path.GetFileNameWithoutExtension(v.Replace('\\', '/')))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            return new ProjectMetadata(projectFile, assembly, tfm, rootNs, refNames, refIncludes);
        }
        catch
        {
            return null;
        }
    }
}
