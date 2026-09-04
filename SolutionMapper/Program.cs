using Spectre.Console;
using SolutionMapper.DiffTools;
using SolutionMapper.Mapping;
using SolutionMapper.UI;

Trace.Log($"build: {typeof(Program).Assembly.GetName().Version} at {typeof(Program).Assembly.Location}");

AppDomain.CurrentDomain.ProcessExit += (_, _) => Metrics.Dump("solution-mapper run");

string? exportPath = null;
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--export")
    {
        if (i + 1 >= args.Length)
        {
            // ponytail: WriteLine — MarkupLine treats [...] as Spectre styles
            AnsiConsole.WriteLine("Error: --export requires a path.");
            return 1;
        }
        exportPath = args[++i];
    }
    else
    {
        positional.Add(args[i]);
    }
}

string legacyRoot;
string upgradedRoot;

if (positional.Count == 2)
{
    legacyRoot = Path.GetFullPath(positional[0]);
    upgradedRoot = Path.GetFullPath(positional[1]);
}
else if (positional.Count > 2)
{
    AnsiConsole.WriteLine("Usage: mapper \"<legacy-root>\" \"<upgraded-root>\" [--export mapping.json]");
    AnsiConsole.WriteLine("Or run with no args to enter paths interactively.");
    return 1;
}
else
{
    AnsiConsole.WriteLine("Solution Mapper — interactive setup");
    AnsiConsole.WriteLine(new string('─', 44));
    AnsiConsole.WriteLine();

    var reused = false;
    if (positional.Count == 0)
    {
        var last = await LastRootsStore.TryLoadAsync();
        if (last is not null)
        {
            AnsiConsole.WriteLine("Last roots:");
            AnsiConsole.WriteLine($"  Legacy:   {last.LegacyRoot}");
            AnsiConsole.WriteLine($"  Upgraded: {last.UpgradedRoot}");
            AnsiConsole.WriteLine();
            if (AnsiConsole.Confirm("Reuse these roots?", true))
            {
                legacyRoot = last.LegacyRoot;
                upgradedRoot = last.UpgradedRoot;
                reused = true;
            }
            else
            {
                legacyRoot = "";
                upgradedRoot = "";
            }
        }
        else
        {
            legacyRoot = "";
            upgradedRoot = "";
        }
    }
    else
    {
        legacyRoot = Path.GetFullPath(positional[0]);
        upgradedRoot = "";
    }

    try
    {
        if (!reused)
        {
            if (string.IsNullOrEmpty(legacyRoot))
                legacyRoot = PathAutocompletePrompt.Prompt(
                    "Legacy solution root:",
                    PathKind.Directory);
            upgradedRoot = PathAutocompletePrompt.Prompt(
                "Upgraded solution root:",
                PathKind.Directory);
        }

        if (exportPath is null && AnsiConsole.Confirm("Export mapping JSON?", false))
        {
            exportPath = PathAutocompletePrompt.Prompt(
                "Export path:",
                PathKind.FileOrDirectory,
                initial: "mapping.json");
        }
    }
    catch (OperationCanceledException)
    {
        AnsiConsole.WriteLine("Canceled.");
        return 1;
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
        return 1;
    }
}

try
{
    if (!Directory.Exists(legacyRoot))
    {
        AnsiConsole.WriteLine($"Error: Legacy solution root does not exist.\n\n  {legacyRoot}");
        return 1;
    }
    if (!Directory.Exists(upgradedRoot))
    {
        AnsiConsole.WriteLine($"Error: Upgraded solution root does not exist.\n\n  {upgradedRoot}");
        return 1;
    }

    await LastRootsStore.SaveAsync(legacyRoot, upgradedRoot);

    AnsiConsole.WriteLine("Solution Mapper");
    AnsiConsole.WriteLine(new string('─', 44));
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Legacy:");
    AnsiConsole.WriteLine($"  {legacyRoot}");
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Upgraded:");
    AnsiConsole.WriteLine($"  {upgradedRoot}");
    AnsiConsole.WriteLine();

    IReadOnlyList<ProjectMapping> mappings = null!;
    ProjectGraph legacyGraph = null!;
    using (Metrics.Measure("pipeline: scan + map + graph"))
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Scanning projects...", async ctx =>
        {
            var legacyScan = SolutionScan.Create(legacyRoot);
            legacyScan.EnsureHasProjects();
            ctx.Status("Scanning upgraded...");
            var upgradedScan = SolutionScan.Create(upgradedRoot);
            upgradedScan.EnsureHasProjects();
            ctx.Status("Mapping projects...");
            mappings = await ProjectMapper.MapAsync(legacyScan, upgradedScan);
            ctx.Status("Building dependency graph...");
            legacyGraph = await ProjectGraph.BuildAsync(legacyScan.ProjectFiles);
            Trace.Log($"legacy graph built from {legacyScan.ProjectFiles.Count} project file(s)");
        });

    if (exportPath is not null)
        await MappingExport.WriteAsync(Path.GetFullPath(exportPath), legacyRoot, upgradedRoot, mappings);

    MappingSummary.Write(mappings);
    AnsiConsole.WriteLine();

    ActionMenu.Run(mappings, legacyRoot, upgradedRoot);

    var selected = ProjectPicker.Pick(mappings, legacyRoot, upgradedRoot, legacyGraph);
    if (selected is null || selected.Count == 0) return 0;

    var tool = DiffToolPicker.Pick(DiffToolDiscovery.GetAvailable());
    var empty = EmptyFolder.GetPath();

    var pairs = selected
        .Select(m => new DiffPair(m.LegacyFolder ?? empty, m.UpgradedFolder ?? empty, m.Name))
        .ToList();

    if (pairs.Count > 1 && !tool.SupportsSingleWindow)
    {
        AnsiConsole.MarkupLine(
            "[yellow]⚠ {0} will open {1} separate windows.[/]", tool.Name, pairs.Count);
        if (!AnsiConsole.Confirm("Continue?", false))
            return 0;
    }

    Trace.Log($"opening {pairs.Count} pair(s) with {tool.Name} (singleWindow={tool.SupportsSingleWindow})");
    tool.OpenMany(pairs);

    return 0;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
    return 1;
}

