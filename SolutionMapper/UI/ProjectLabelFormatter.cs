using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class ProjectLabelFormatter
{
    public static string Format(ProjectMapping m, string legacyRoot, string upgradedRoot)
    {
        var status = m.Status switch
        {
            MappingStatus.Ambiguous when m.IsOneToMany => "  (1→N)",
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

    public static bool MatchesFilter(
        ProjectMapping m, string filter, string legacyRoot, string upgradedRoot)
    {
        if (m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        var label = Format(m, legacyRoot, upgradedRoot);
        if (label.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (m.LegacyFolder?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (m.UpgradedFolder?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return false;
    }

    static string PathHint(ProjectMapping m, string legacyRoot, string upgradedRoot)
    {
        var hasL = !string.IsNullOrEmpty(m.LegacyFolder);
        var hasU = !string.IsNullOrEmpty(m.UpgradedFolder);

        if (hasL && hasU)
            return $"L:{ShortPath(legacyRoot, m.LegacyFolder!)} → U:{ShortPath(upgradedRoot, m.UpgradedFolder!)}";
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
