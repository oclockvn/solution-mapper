using SolutionMapper.UI;

namespace SolutionMapper.Mapping;

/// <summary>
/// One recursive <c>*.csproj</c> scan of a solution tree, reused across the pipeline
/// (mapper, dependency graph) so the directory walk happens exactly once per root.
/// </summary>
public sealed class SolutionScan
{
    public string Root { get; }
    public IReadOnlyList<string> ProjectFiles { get; }

    SolutionScan(string root, IReadOnlyList<string> projectFiles)
    {
        Root = root;
        ProjectFiles = projectFiles;
    }

    public static SolutionScan Create(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Error: solution root does not exist.\n\n  {root}");

        using var _ = Metrics.Measure("SolutionScan.Create (recursive scan, once per root)");
        var files = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).ToList();
        Metrics.Count("csproj files enumerated", files.Count);
        return new SolutionScan(root, files);
    }

    public void EnsureHasProjects()
    {
        if (ProjectFiles.Count == 0)
            throw new InvalidOperationException($"Error: No .csproj files found under:\n\n  {Root}");
    }
}
