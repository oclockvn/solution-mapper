using System.Collections.Concurrent;
using SolutionMapper.UI;

namespace SolutionMapper.Mapping;

public static class ProjectMetadataReader
{
    // ponytail: a .csproj is parsed once per process; the pipeline reads each file
    // several times (mapper M×N groups, dependency graph). Keyed by full path.
    static readonly ConcurrentDictionary<string, ProjectMetadata?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cached read. Same result as <see cref="TryRead"/>, parsed at most once per path.</summary>
    public static ProjectMetadata? Read(string projectFile)
    {
        var key = Path.GetFullPath(projectFile);
        if (Cache.TryGetValue(key, out var hit))
        {
            Metrics.Count("TryRead cache hit");
            return hit;
        }
        var meta = TryRead(projectFile);
        Cache[key] = meta;
        return meta;
    }

    public static ProjectMetadata? TryRead(string projectFile)
    {
        using var _ = Metrics.Measure("ProjectMetadataReader.TryRead (XDocument.Load + walk)");
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(projectFile);
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
