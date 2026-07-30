namespace SolutionMapper.Mapping;

public static class ProjectDiscovery
{
    public static IReadOnlyList<string> FindProjects(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Error: solution root does not exist.\n\n  {root}");

        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).ToList();
    }

    public static void EnsureHasProjects(string root, string label)
    {
        var projects = FindProjects(root);
        if (projects.Count == 0)
            throw new InvalidOperationException($"Error: No .csproj files found under:\n\n  {root}");
    }
}
