using Spectre.Console;

namespace SolutionMapper.UI;

/// <summary>
/// Lightweight step logger for investigating the interactive flow.
/// Enable with environment variable <c>SOLUTIONMAPPER_TRACE=1</c> (or <c>true</c>).
/// </summary>
public static class Trace
{
    public static bool Enabled { get; } =
        (Environment.GetEnvironmentVariable("SOLUTIONMAPPER_TRACE") ?? "")
            .Trim() is "1" or "true" or "TRUE" or "yes";

    public static void Log(string message)
    {
        if (!Enabled) return;
        AnsiConsole.MarkupLine($"[grey]· trace:[/] {message.EscapeMarkup()}");
    }
}
