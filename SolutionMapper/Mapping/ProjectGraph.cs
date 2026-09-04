namespace SolutionMapper.Mapping;

/// <summary>
/// Project-reference dependency graph for one solution tree, keyed by absolute
/// <c>.csproj</c> path. Edges are resolved from each project's raw
/// <c>&lt;ProjectReference Include="..." /&gt;</c> relative to the referencing file.
/// </summary>
public sealed class ProjectGraph
{
    // key: absolute .csproj path (case-insensitive). value: resolved dependency .csproj paths.
    readonly Dictionary<string, List<string>> _edges;
    readonly HashSet<string> _known;

    ProjectGraph(Dictionary<string, List<string>> edges, HashSet<string> known)
    {
        _edges = edges;
        _known = known;
    }

    public static ProjectGraph Build(IReadOnlyList<string> projectFiles)
    {
        using var _ = UI.Metrics.Measure("ProjectGraph.Build (total, incl. TryRead per project)");
        var known = new HashSet<string>(
            projectFiles.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        // filename -> full paths, for path-changed fallback resolution
        var byName = projectFiles
            .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(Path.GetFullPath).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var edges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectFile in projectFiles)
        {
            var full = Path.GetFullPath(projectFile);
            var meta = ProjectMetadataReader.Read(full);
            var deps = new List<string>();

            if (meta is not null)
            {
                var dir = Path.GetDirectoryName(full)!;
                foreach (var include in meta.ProjectReferencePaths)
                {
                    var resolved = Resolve(dir, include, known, byName);
                    if (resolved is not null && !deps.Contains(resolved, StringComparer.OrdinalIgnoreCase))
                        deps.Add(resolved);
                }
            }

            edges[full] = deps;
        }

        return new ProjectGraph(edges, known);
    }

    static string? Resolve(
        string referencingDir,
        string include,
        HashSet<string> known,
        Dictionary<string, List<string>> byName)
    {
        var normalized = include.Replace('\\', Path.DirectorySeparatorChar)
                                .Replace('/', Path.DirectorySeparatorChar);
        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(referencingDir, normalized));
        }
        catch
        {
            candidate = "";
        }

        if (candidate.Length > 0 && known.Contains(candidate))
            return candidate;

        // path layout changed in this tree: fall back to a unique same-named project.
        var fileName = Path.GetFileName(normalized);
        if (fileName.Length > 0 && byName.TryGetValue(fileName, out var matches) && matches.Count == 1)
            return matches[0];

        return null;
    }

    /// <summary>
    /// Transitive dependency closure of <paramref name="rootProjectFile"/>, including the root.
    /// Breadth-first, cycle-safe. Returns absolute <c>.csproj</c> paths.
    /// </summary>
    public IReadOnlyList<string> Closure(string rootProjectFile)
    {
        var root = Path.GetFullPath(rootProjectFile);
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(root);
        seen.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);
            if (!_edges.TryGetValue(current, out var deps)) continue;
            foreach (var dep in deps)
            {
                if (seen.Add(dep))
                    queue.Enqueue(dep);
            }
        }

        return order;
    }

    /// <summary>Direct (non-transitive) resolved dependencies of a project.</summary>
    public IReadOnlyList<string> DirectDependencies(string projectFile) =>
        _edges.TryGetValue(Path.GetFullPath(projectFile), out var deps) ? deps : [];
}
