namespace SolutionMapper.Mapping;

/// <summary>One node of an expanded dependency closure: a mapping plus its depth from the root.</summary>
public sealed record ClosureNode(ProjectMapping Mapping, int Depth, bool IsRoot);

/// <summary>
/// Expands a selected mapping into its transitive project-reference closure, driven by the
/// legacy dependency graph and translated to <see cref="ProjectMapping"/>s via each node's
/// legacy <c>.csproj</c> path.
/// </summary>
public static class DependencyClosure
{
    public sealed record Result(
        IReadOnlyList<ClosureNode> Nodes,
        IReadOnlyList<string> UnresolvedLegacyRefs);

    /// <param name="root">The mapping the user picked. Must have a <see cref="ProjectMapping.LegacyProjectFile"/>.</param>
    /// <param name="graph">Graph built from the legacy project files.</param>
    /// <param name="allMappings">Every mapping from <see cref="ProjectMapper.Map"/>.</param>
    /// <param name="maxDepth">
    /// Deepest reference level to include, counted from the root (root = 0, direct refs = 1).
    /// <c>null</c> or a negative value means no limit.
    /// </param>
    public static Result Expand(
        ProjectMapping root,
        ProjectGraph graph,
        IReadOnlyList<ProjectMapping> allMappings,
        int? maxDepth = null)
    {
        var byLegacyFile = new Dictionary<string, ProjectMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in allMappings)
        {
            if (m.LegacyProjectFile is null) continue;
            var key = Path.GetFullPath(m.LegacyProjectFile);
            // first wins; fan-out mappings share a legacy file, any counterpart is a fine entry point
            byLegacyFile.TryAdd(key, m);
        }

        if (root.LegacyProjectFile is null)
            return new Result([new ClosureNode(root, 0, true)], []);

        var rootFile = Path.GetFullPath(root.LegacyProjectFile);
        var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [rootFile] = 0 };
        var closure = graph.Closure(rootFile);

        // BFS depth: recompute so indentation reflects the shortest path from the root
        var queue = new Queue<string>();
        queue.Enqueue(rootFile);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var d = depth[current];
            foreach (var dep in graph.DirectDependencies(current))
            {
                if (depth.TryAdd(dep, d + 1))
                    queue.Enqueue(dep);
            }
        }

        var limit = maxDepth is { } md && md >= 0 ? md : int.MaxValue;

        var nodes = new List<ClosureNode>();
        var unresolved = new List<string>();

        foreach (var file in closure)
        {
            var d = depth.TryGetValue(file, out var dv) ? dv : 1;
            if (d > limit) continue;
            if (byLegacyFile.TryGetValue(file, out var mapping))
                nodes.Add(new ClosureNode(mapping, d, file.Equals(rootFile, StringComparison.OrdinalIgnoreCase)));
            else
                unresolved.Add(file);
        }

        nodes = nodes
            .OrderBy(n => n.Depth)
            .ThenBy(n => n.Mapping.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Result(nodes, unresolved);
    }
}
