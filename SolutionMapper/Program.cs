using Spectre.Console;
using SolutionMapper.DiffTools;
using SolutionMapper.Mapping;
using SolutionMapper.UI;

if (args.Length < 2)
{
    AnsiConsole.MarkupLine("Usage: mapper \"<legacy-root>\" \"<upgraded-root>\" [--export mapping.json]");
    return 1;
}

string? exportPath = null;
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--export")
    {
        if (i + 1 >= args.Length)
        {
            AnsiConsole.MarkupLine("Error: --export requires a path.");
            return 1;
        }
        exportPath = args[++i];
    }
    else
    {
        positional.Add(args[i]);
    }
}

if (positional.Count != 2)
{
    AnsiConsole.MarkupLine("Usage: mapper \"<legacy-root>\" \"<upgraded-root>\" [--export mapping.json]");
    return 1;
}

var legacyRoot = Path.GetFullPath(positional[0]);
var upgradedRoot = Path.GetFullPath(positional[1]);

try
{
    if (!Directory.Exists(legacyRoot))
    {
        AnsiConsole.MarkupLine($"Error: Legacy solution root does not exist.\n\n  {legacyRoot}");
        return 1;
    }
    if (!Directory.Exists(upgradedRoot))
    {
        AnsiConsole.MarkupLine($"Error: Upgraded solution root does not exist.\n\n  {upgradedRoot}");
        return 1;
    }

    AnsiConsole.WriteLine("Solution Mapper");
    AnsiConsole.WriteLine(new string('─', 44));
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Legacy:");
    AnsiConsole.WriteLine($"  {legacyRoot}");
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Upgraded:");
    AnsiConsole.WriteLine($"  {upgradedRoot}");
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Scanning projects...");
    AnsiConsole.WriteLine();

    ProjectDiscovery.EnsureHasProjects(legacyRoot, "Legacy");
    ProjectDiscovery.EnsureHasProjects(upgradedRoot, "Upgraded");

    var mappings = ProjectMapper.Map(legacyRoot, upgradedRoot);

    if (exportPath is not null)
        MappingExport.Write(Path.GetFullPath(exportPath), legacyRoot, upgradedRoot, mappings);

    MappingSummary.Write(mappings);
    AnsiConsole.WriteLine();

    var selected = ProjectPicker.Pick(mappings);
    if (selected is null || selected.Count == 0) return 0;

    var tool = DiffToolPicker.Pick(DiffToolDiscovery.GetAvailable());
    var empty = EmptyFolder.GetPath();

    foreach (var m in selected)
    {
        var left = m.LegacyFolder ?? empty;
        var right = m.UpgradedFolder ?? empty;
        tool.Open(left, right);
    }

    return 0;
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
    return 1;
}
