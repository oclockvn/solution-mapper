using Spectre.Console;
using SolutionMapper.DiffTools;

namespace SolutionMapper.UI;

public static class DiffToolPicker
{
    public static IDiffTool Pick(IReadOnlyList<IDiffTool> tools)
    {
        if (tools.Count == 0)
            throw new InvalidOperationException(
                "No supported diff tools were found (WinMerge, Beyond Compare, VS Code).");

        return AnsiConsole.Prompt(
            new SelectionPrompt<IDiffTool>()
                .Title("Select comparison tool:")
                .UseConverter(t => t.Name)
                .AddChoices(tools));
    }
}
