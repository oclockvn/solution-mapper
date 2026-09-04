using System.Runtime.CompilerServices;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class ProjectLabelFormatter
{
    // The display label and search text for a mapping never change once mapping is done,
    // but the interactive picker recomputes them on every keystroke-filter and on every
    // Spectre list redraw. Cache per mapping instance (roots are constant within a run).
    sealed record Cached(string Label, string SearchBlob);

    static readonly ConditionalWeakTable<ProjectMapping, Cached> LabelCache = new();

    static Cached GetCached(ProjectMapping m, string legacyRoot, string upgradedRoot) =>
        LabelCache.GetValue(m, key =>
        {
            using var _ = Metrics.Measure("ProjectLabelFormatter build (cache miss)");
            var label = BuildLabel(key, legacyRoot, upgradedRoot);
            var blob = string.Join('\n', new[]
                {
                    key.Name, label, key.LegacyFolder, key.UpgradedFolder
                }
                .Where(s => !string.IsNullOrEmpty(s)))
                .ToLowerInvariant();
            return new Cached(label, blob);
        });

    public static string Format(ProjectMapping m, string legacyRoot, string upgradedRoot) =>
        GetCached(m, legacyRoot, upgradedRoot).Label;

    public static bool MatchesFilter(
        ProjectMapping m, string filter, string legacyRoot, string upgradedRoot)
    {
        using var _ = Metrics.Measure("ProjectLabelFormatter.MatchesFilter (per call)");
        var blob = GetCached(m, legacyRoot, upgradedRoot).SearchBlob;
        return blob.Contains(filter.ToLowerInvariant(), StringComparison.Ordinal);
    }

    static string BuildLabel(ProjectMapping m, string legacyRoot, string upgradedRoot)
    {
        var status = m.Status switch
        {
            MappingStatus.Ambiguous when m.IsOneToMany => "  (1:N)",
            MappingStatus.Ambiguous => "  (ambiguous)",
            MappingStatus.LegacyOnly => "  legacy only",
            MappingStatus.UpgradedOnly => "  upgraded only",
            _ => ""
        };

        var hint = PathHint(m, legacyRoot, upgradedRoot);
        return string.IsNullOrEmpty(hint)
            ? $"{m.Name}{status}"
            : $"{m.Name}{status}  {hint}";
    }

    static string PathHint(ProjectMapping m, string legacyRoot, string upgradedRoot)
    {
        var hasL = !string.IsNullOrEmpty(m.LegacyFolder);
        var hasU = !string.IsNullOrEmpty(m.UpgradedFolder);

        if (hasL && hasU)
            return $"L:{ShortPath(legacyRoot, m.LegacyFolder!)} -> U:{ShortPath(upgradedRoot, m.UpgradedFolder!)}";
        if (hasL)
            return $"L:{ShortPath(legacyRoot, m.LegacyFolder!)}";
        if (hasU)
            return $"U:{ShortPath(upgradedRoot, m.UpgradedFolder!)}";
        return "";
    }

    public static string ShortPath(string root, string folder)
    {
        try
        {
            var rel = Path.GetRelativePath(root, folder);
            if (!rel.StartsWith("..", StringComparison.Ordinal) && rel != ".")
                return rel;
        }
        catch
        {
            // fall through to tail segments
        }

        var parts = folder.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3) return folder;
        return string.Join(Path.DirectorySeparatorChar, parts[^3..]);
    }
}
