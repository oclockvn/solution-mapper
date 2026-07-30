using Spectre.Console;
using SolutionMapper.DiffTools;
using SolutionMapper.Mapping;
using SolutionMapper.UI;

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
    // Interactive mode when roots missing
    AnsiConsole.WriteLine("Solution Mapper — interactive setup");
    AnsiConsole.WriteLine(new string('─', 44));
    AnsiConsole.WriteLine();

    legacyRoot = positional.Count == 1
        ? Path.GetFullPath(positional[0])
        : PromptExistingDirectory("Legacy solution root:");

    upgradedRoot = PromptExistingDirectory("Upgraded solution root:");

    if (exportPath is null && AnsiConsole.Confirm("Export mapping JSON?", false))
    {
        exportPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Export path:")
                .DefaultValue("mapping.json"));
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

static string PromptExistingDirectory(string title)
{
    var path = AnsiConsole.Prompt(
        new TextPrompt<string>(title)
            .Validate(p =>
            {
                if (string.IsNullOrWhiteSpace(p))
                    return ValidationResult.Error("Path is required.");
                return Directory.Exists(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Directory does not exist.");
            }));
    return Path.GetFullPath(path);
}
