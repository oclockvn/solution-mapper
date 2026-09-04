using SolutionMapper.UI;

namespace SolutionMapper.Mapping;

public static class ProjectMatcher
{
    public static int Score(ProjectMetadata? left, ProjectMetadata? right, string fileName)
    {
        using var _ = Metrics.Measure("ProjectMatcher.Score (per pair)");
        var score = 100; // same filename group
        if (left is null || right is null) return score;

        if (Eq(left.AssemblyName, right.AssemblyName)) score += 50;
        if (Eq(left.TargetFramework, right.TargetFramework)) score += 10;
        if (Eq(left.RootNamespace, right.RootNamespace)) score += 5;

        var overlap = left.ProjectReferenceNames.Intersect(
            right.ProjectReferenceNames, StringComparer.OrdinalIgnoreCase).Any();
        if (overlap) score += 10;

        return score;

        static bool Eq(string? a, string? b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
            && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
