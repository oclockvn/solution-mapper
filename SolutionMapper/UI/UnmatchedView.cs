using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

/// <summary>
/// Read-only "list unmatched projects" view: every <see cref="MappingStatus.LegacyOnly"/> and
/// <see cref="MappingStatus.UpgradedOnly"/> mapping, grouped by side, one line each, showing the
/// <c>.csproj</c> path relative to that side's root.
/// </summary>
public static class UnmatchedView
{
    /// <summary>
    /// Builds the plain-text lines for the view (no Spectre markup), so the layout is unit-testable.
    /// <paramref name="legacyRoot"/> / <paramref name="upgradedRoot"/> are the scan roots each side's
    /// paths are made relative to.
    /// </summary>
    public static IReadOnlyList<string> BuildLines(
        IReadOnlyList<ProjectMapping> mappings, string legacyRoot, string upgradedRoot)
    {
        var legacy = Section(mappings, MappingStatus.LegacyOnly, m => m.LegacyProjectFile, legacyRoot);
        var upgraded = Section(mappings, MappingStatus.UpgradedOnly, m => m.UpgradedProjectFile, upgradedRoot);

        var lines = new List<string>
        {
            "Unmatched projects",
            new('─', 44),
            "",
            $"LEGACY ONLY ({legacy.Count}) — in legacy, no match in upgraded",
            ""
        };
        AppendSection(lines, legacy);

        lines.Add("");
        lines.Add($"UPGRADED ONLY ({upgraded.Count}) — in upgraded, no match in legacy");
        lines.Add("");
        AppendSection(lines, upgraded);

        return lines;
    }

    /// <summary>Prints the view, then waits for Enter.</summary>
    public static void Show(
        IReadOnlyList<ProjectMapping> mappings, string legacyRoot, string upgradedRoot)
    {
        foreach (var line in BuildLines(mappings, legacyRoot, upgradedRoot))
            AnsiConsole.WriteLine(line);

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(new string('─', 44));
        AnsiConsole.Prompt(new TextPrompt<string>("[grey][[Enter]] back to menu[/]").AllowEmpty());
    }

    static List<string> Section(
        IReadOnlyList<ProjectMapping> mappings,
        MappingStatus status,
        Func<ProjectMapping, string?> file,
        string root) =>
        mappings
            .Where(m => m.Status == status)
            .Select(m => Rel(root, file(m)))
            .Where(p => p.Length > 0)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    static void AppendSection(List<string> lines, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            lines.Add("  (none)");
            return;
        }
        foreach (var p in paths)
            lines.Add("  " + p);
    }

    static string Rel(string root, string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return "";
        try
        {
            var rel = Path.GetRelativePath(root, fullPath);
            return rel.StartsWith("..", StringComparison.Ordinal) ? fullPath : rel;
        }
        catch
        {
            return fullPath;
        }
    }
}
