using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class MappingSummary
{
    public static void Write(IReadOnlyList<ProjectMapping> mappings)
    {
        var matched = mappings.Count(m => m.Status == MappingStatus.Matched);
        var ambiguous = mappings.Count(m => m.Status == MappingStatus.Ambiguous || m.IsAmbiguous);
        var legacyOnly = mappings.Count(m => m.Status == MappingStatus.LegacyOnly);
        var upgradedOnly = mappings.Count(m => m.Status == MappingStatus.UpgradedOnly);

        AnsiConsole.MarkupLine("[green]✓[/] {0} matched", matched);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} ambiguous", ambiguous);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} legacy only", legacyOnly);
        AnsiConsole.MarkupLine("[yellow]⚠[/] {0} upgraded only", upgradedOnly);
    }
}
