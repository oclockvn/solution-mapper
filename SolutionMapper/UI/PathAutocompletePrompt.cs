using Spectre.Console;

namespace SolutionMapper.UI;

public static class PathAutocompletePrompt
{
    public static string Prompt(
        string title,
        PathKind kind,
        Func<string, ValidationResult>? validate = null,
        string initial = "")
    {
        validate ??= path => ValidateDefault(path, kind);

        if (Console.IsInputRedirected)
        {
            var prompt = new TextPrompt<string>(title).Validate(validate);
            if (!string.IsNullOrEmpty(initial))
                prompt.DefaultValue(initial);

            return Path.GetFullPath(AnsiConsole.Prompt(prompt));
        }

        AnsiConsole.WriteLine(title);

        var buffer = initial;
        var renderedLength = 0;
        Render(buffer, ref renderedLength);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape ||
                key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                AnsiConsole.WriteLine();
                throw new OperationCanceledException();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                    buffer = buffer[..^1];
                Render(buffer, ref renderedLength);
                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                var suggestions = PathCompletion.GetSuggestions(buffer, kind);
                if (suggestions.Count == 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]No matches.[/]");
                }
                else if (suggestions.Count == 1)
                {
                    buffer = suggestions[0];
                }
                else
                {
                    AnsiConsole.WriteLine();
                    var selectionTitle = suggestions.Count == PathCompletion.DefaultMax
                        ? $"Select path (showing {PathCompletion.DefaultMax}):"
                        : "Select path:";
                    buffer = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title(selectionTitle)
                            .UseConverter(path => path.EscapeMarkup())
                            .AddChoices(suggestions)
                            .AddCancelResult(() => throw new OperationCanceledException()));
                }

                Render(buffer, ref renderedLength);
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                AnsiConsole.WriteLine();
                var result = validate(buffer);
                if (!result.Successful)
                {
                    AnsiConsole.MarkupLine($"[red]{(result.Message ?? "Invalid path.").EscapeMarkup()}[/]");
                    Render(buffer, ref renderedLength);
                    continue;
                }

                try
                {
                    return Path.GetFullPath(buffer);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    AnsiConsole.MarkupLine($"[red]{ex.Message.EscapeMarkup()}[/]");
                    Render(buffer, ref renderedLength);
                    continue;
                }
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer += key.KeyChar;
                Render(buffer, ref renderedLength);
            }
        }
    }

    private static ValidationResult ValidateDefault(string path, PathKind kind)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Error("Path is required.");

        if (kind == PathKind.Directory && !Directory.Exists(path))
            return ValidationResult.Error("Directory does not exist.");

        return ValidationResult.Success();
    }

    private static void Render(string buffer, ref int renderedLength)
    {
        var line = $"> {buffer}";
        AnsiConsole.Write(new Text($"\r{line}{new string(' ', Math.Max(0, renderedLength - line.Length))}"));
        renderedLength = line.Length;
    }
}
