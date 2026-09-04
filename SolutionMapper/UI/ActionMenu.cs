using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

/// <summary>
/// The "What now?" step between the summary and the search picker. Loops on "List unmatched
/// projects"; returns once the user picks "Search &amp; diff projects" so the caller runs the
/// normal picker flow.
/// </summary>
public static class ActionMenu
{
    enum Choice { Search, ListUnmatched }

    /// <summary>
    /// Shows the menu (unless there are no unmatched projects to list, in which case it's skipped).
    /// Returns when the user chooses to search; the caller then proceeds to <see cref="ProjectPicker"/>.
    /// </summary>
    public static void Run(IReadOnlyList<ProjectMapping> mappings, string legacyRoot, string upgradedRoot)
    {
        if (MappingSummary.UnmatchedCount(mappings) == 0)
        {
            Trace.Log("ActionMenu: no unmatched projects -> skipping menu");
            return;
        }

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<Choice>()
                    .Title("What now?")
                    .UseConverter(c => c switch
                    {
                        Choice.Search => "Search & diff projects",
                        Choice.ListUnmatched => "List unmatched projects",
                        _ => c.ToString()
                    })
                    .AddChoices(Choice.Search, Choice.ListUnmatched));

            Trace.Log($"ActionMenu: chose {choice}");
            if (choice == Choice.Search) return;

            UnmatchedView.Show(mappings, legacyRoot, upgradedRoot);
        }
    }
}
