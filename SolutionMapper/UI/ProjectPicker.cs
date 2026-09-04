using Spectre.Console;
using SolutionMapper.Mapping;

namespace SolutionMapper.UI;

public static class ProjectPicker
{
    public static IReadOnlyList<ProjectMapping>? Pick(
        IReadOnlyList<ProjectMapping> mappings,
        string legacyRoot,
        string upgradedRoot,
        ProjectGraph? legacyGraph = null)
    {
        Trace.Log($"Pick: {mappings.Count} mappings, legacyGraph={(legacyGraph is null ? "NULL" : "present")}");
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

            Trace.Log($"filter='{filter}' -> {filtered.Count} shown");
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<ProjectMapping>()
                    .Title($"Select projects ({filtered.Count} shown):")
                    .NotRequired()
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up/down to reveal more)[/]")
                    .InstructionsText("[grey](Space to toggle, enter to confirm)[/]")
                    .UseConverter(m => ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot))
                    .AddChoices(filtered));

            Trace.Log($"multi-select returned {selected.Count} item(s): {string.Join(", ", selected.Select(m => m.Name))}");
            if (selected.Count > 0)
            {
                var expanded = MaybeExpandDependencies(selected, mappings, legacyGraph, legacyRoot, upgradedRoot);
                Trace.Log($"Pick returning {expanded.Count} project(s)");
                return expanded;
            }

            if (!AnsiConsole.Confirm("Nothing selected. Search again?", true)) return selected;
        }
    }

    static IReadOnlyList<ProjectMapping> MaybeExpandDependencies(
        IReadOnlyList<ProjectMapping> selected,
        IReadOnlyList<ProjectMapping> allMappings,
        ProjectGraph? legacyGraph,
        string legacyRoot,
        string upgradedRoot)
    {
        if (legacyGraph is null)
        {
            Trace.Log("MaybeExpandDependencies: legacyGraph is null -> skipping closure prompt");
            return selected;
        }

        var withLegacy = selected.Count(m => m.LegacyProjectFile is not null);
        Trace.Log($"MaybeExpandDependencies: {withLegacy}/{selected.Count} selected have a legacy .csproj");
        if (withLegacy == 0)
        {
            Trace.Log("MaybeExpandDependencies: no selected project has a legacy file -> skipping closure prompt");
            return selected;
        }

        if (!AnsiConsole.Confirm("Include transitive project dependencies?", false))
        {
            Trace.Log("MaybeExpandDependencies: user declined closure expansion");
            return selected;
        }

        var maxDepth = AskDepth();
        Trace.Log($"MaybeExpandDependencies: maxDepth={(maxDepth is null ? "unlimited" : maxDepth.ToString())}");

        // ordered union of every root's closure, at the chosen depth
        var closureNodes = new List<ClosureNode>();
        var seenNode = new HashSet<ProjectMapping>();
        var allUnresolved = new List<string>();

        foreach (var root in selected)
        {
            var expansion = DependencyClosure.Expand(root, legacyGraph, allMappings, maxDepth);
            Trace.Log($"closure of '{root.Name}': {expansion.Nodes.Count} node(s), {expansion.UnresolvedLegacyRefs.Count} unresolved");
            allUnresolved.AddRange(expansion.UnresolvedLegacyRefs);
            foreach (var node in expansion.Nodes)
            {
                if (seenNode.Add(node.Mapping))
                    closureNodes.Add(node);
            }
        }

        // explicitly-selected mappings with no legacy file (upgraded-only) are always kept
        foreach (var m in selected)
        {
            if (seenNode.Add(m))
                closureNodes.Add(new ClosureNode(m, 0, true));
        }

        ReportUnresolved(allUnresolved, legacyRoot);

        if (closureNodes.Count == 0)
            return selected;

        // let the user prune the closure; roots + matched nodes are pre-checked, missing ones too
        // (so the gap is visible in the diff), ambiguous left unchecked as they need a manual look.
        var prompt = new MultiSelectionPrompt<ClosureNode>()
            .Title($"Closure — pick what to diff ({closureNodes.Count} projects):")
            .NotRequired()
            .PageSize(20)
            .MoreChoicesText("[grey](Move up/down to reveal more)[/]")
            .InstructionsText("[grey](Space toggles, Enter confirms — depth shown in brackets)[/]")
            .UseConverter(n => ClosureLabel(n, legacyRoot, upgradedRoot));

        foreach (var n in closureNodes)
        {
            var preselect = n.IsRoot
                || n.Mapping.Status is MappingStatus.Matched
                    or MappingStatus.LegacyOnly or MappingStatus.UpgradedOnly;
            prompt.AddChoices(n, item =>
            {
                if (preselect) item.Select();
            });
        }

        var curated = AnsiConsole.Prompt(prompt);

        Trace.Log($"curated closure: {curated.Count} of {closureNodes.Count}");
        if (curated.Count == 0)
            return selected;

        return curated.Select(n => n.Mapping).ToList();
    }

    static int? AskDepth()
    {
        var answer = AnsiConsole.Prompt(
            new TextPrompt<string>("Max dependency depth ([grey]blank = all, 1 = direct refs only[/]):")
                .AllowEmpty()
                .Validate(s =>
                    string.IsNullOrWhiteSpace(s) || (int.TryParse(s, out var v) && v >= 0)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Enter a non-negative number or leave blank[/]")));

        return string.IsNullOrWhiteSpace(answer) ? null : int.Parse(answer);
    }

    static void ReportUnresolved(IReadOnlyList<string> unresolved, string legacyRoot)
    {
        var distinct = unresolved
            .Select(f => ProjectLabelFormatter.ShortPath(legacyRoot, Path.GetDirectoryName(f) ?? f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinct.Count == 0) return;

        AnsiConsole.MarkupLine("[yellow]⚠ {0} referenced project(s) not found under the legacy root (skipped):[/]",
            distinct.Count);
        foreach (var u in distinct)
            AnsiConsole.MarkupLine("  [grey]{0}[/]", u.EscapeMarkup());
    }

    static string ClosureLabel(ClosureNode n, string legacyRoot, string upgradedRoot)
    {
        var depth = n.IsRoot ? "root" : $"L{n.Depth}";
        return $"[grey]{depth}[/]  {Label(n.Mapping, legacyRoot, upgradedRoot)}";
    }

    static string Label(ProjectMapping m, string legacyRoot, string upgradedRoot)
    {
        var badge = m.Status switch
        {
            MappingStatus.Matched => "[green]✓[/]",
            MappingStatus.Ambiguous => "[yellow]?[/]",
            MappingStatus.LegacyOnly => "[red]✗ legacy only[/]",
            MappingStatus.UpgradedOnly => "[red]✗ upgraded only[/]",
            _ => " "
        };
        return $"{badge} {ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot).EscapeMarkup()}";
    }
}
