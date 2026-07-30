using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class ProjectPicker
{
    public static IReadOnlyList<ProjectMapping>? Pick(IReadOnlyList<ProjectMapping> mappings)
    {
        if (mappings.Count == 0) return [];

        while (true)
        {
            var filter = AnsiConsole.Prompt(
                new TextPrompt<string>("Search projects (empty = all, Ctrl+C exit):").AllowEmpty());
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? mappings
                : mappings.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
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
                    .UseConverter(Format)
                    .AddChoices(filtered));

            if (selected.Count > 0) return selected;
            if (!AnsiConsole.Confirm("Nothing selected. Search again?", true)) return selected;
        }
    }

    static string Format(ProjectMapping m) => m.Status switch
    {
        MappingStatus.Matched => m.Name,
        MappingStatus.Ambiguous => $"{m.Name}  [ambiguous]",
        MappingStatus.LegacyOnly => $"{m.Name}  legacy only",
        MappingStatus.UpgradedOnly => $"{m.Name}  upgraded only",
        _ => m.Name
    };
}
