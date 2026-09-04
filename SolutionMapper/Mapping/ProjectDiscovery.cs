namespace SolutionMapper.Mapping;

/// <summary>
/// Thin convenience wrapper over <see cref="SolutionScan"/> for callers that only need
/// the project list. The production pipeline uses <see cref="SolutionScan"/> directly so
/// the recursive walk runs once per root.
/// </summary>
public static class ProjectDiscovery
{
    public static IReadOnlyList<string> FindProjects(string root) =>
        SolutionScan.Create(root).ProjectFiles;

    public static void EnsureHasProjects(string root, string label) =>
        SolutionScan.Create(root).EnsureHasProjects();
}
