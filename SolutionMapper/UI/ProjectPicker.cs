using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class ProjectPicker
{
    public static IReadOnlyList<ProjectMapping>? Pick(
        IReadOnlyList<ProjectMapping> mappings,
        string legacyRoot,
        string upgradedRoot)
    {
        if (mappings.Count == 0) return [];

        while (true)
        {
            var filter = AnsiConsole.Prompt(
                new TextPrompt<string>("Search projects (empty = all, Ctrl+C exit):").AllowEmpty());
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? mappings
                : mappings
                    .Where(m => ProjectLabelFormatter.MatchesFilter(m, filter, legacyRoot, upgradedRoot))
                    .ToList();
            if (filtered.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No matches[/]");
                continue;
            }

            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<ProjectMapping>()
                    .Title($"Select projects ({filtered.Count} shown):")
                    .NotRequired()
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up/down to reveal more)[/]")
                    .InstructionsText("[grey](Space to toggle, enter to confirm)[/]")
                    .UseConverter(m => ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot))
                    .AddChoices(filtered));

            if (selected.Count > 0) return selected;
            if (!AnsiConsole.Confirm("Nothing selected. Search again?", true)) return selected;
        }
    }
}
