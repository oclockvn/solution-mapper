using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class MappingSummary
{
    /// <summary>Count of projects present on only one side (legacy-only + upgraded-only).</summary>
    public static int UnmatchedCount(IReadOnlyList<ProjectMapping> mappings) =>
        mappings.Count(m => m.Status is MappingStatus.LegacyOnly or MappingStatus.UpgradedOnly);

    public static void Write(IReadOnlyList<ProjectMapping> mappings)
    {
        var matched = mappings.Count(m => m.Status == MappingStatus.Matched);
        var oneToMany = mappings.Count(m => m.IsOneToMany);
        var ambiguous = mappings.Count(m => (m.Status == MappingStatus.Ambiguous || m.IsAmbiguous) && !m.IsOneToMany);
        var legacyOnly = mappings.Count(m => m.Status == MappingStatus.LegacyOnly);
        var upgradedOnly = mappings.Count(m => m.Status == MappingStatus.UpgradedOnly);

        AnsiConsole.MarkupLine("[green]✓[/] {0} matched", matched);
        if (oneToMany > 0)
            AnsiConsole.MarkupLine("[yellow]⚠[/] {0} one-to-many (shared copies)", oneToMany);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} ambiguous", ambiguous);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} legacy only", legacyOnly);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} upgraded only", upgradedOnly);
    }
}
