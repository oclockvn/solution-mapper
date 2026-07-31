namespace SolutionMapper.UI;

public readonly record struct PathParseResult(string? ParentDirectory, string Prefix, bool SuggestDrives);

public static class PathCompletion
{
    public const int DefaultMax = 50;

    public static PathParseResult Parse(string buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer))
            return new PathParseResult(null, "", true);

        if (buffer.EndsWith('\\') || buffer.EndsWith('/'))
            return new PathParseResult(NormalizeSeparators(buffer), "", false);

        var parent = Path.GetDirectoryName(buffer);
        var prefix = Path.GetFileName(buffer);

        if (parent is null && buffer.Length >= 2 && buffer[1] == ':')
        {
            parent = buffer[..2] + Path.DirectorySeparatorChar;
            prefix = buffer.Length > 2 ? buffer[2..] : "";
        }

        return new PathParseResult(
            parent is null ? null : NormalizeSeparators(parent),
            prefix ?? "",
            false);
    }

    /// <summary>Suggestions are complete path prefixes to replace the buffer (dirs end with \).</summary>
    public static IReadOnlyList<string> GetSuggestions(string buffer, PathKind kind, int max = DefaultMax)
    {
        var parse = Parse(buffer);
        if (parse.SuggestDrives)
        {
            return Environment.GetLogicalDrives()
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        if (parse.ParentDirectory is null)
            return [];

        var results = new List<string>();
        EnumerateSuggestions(parse.ParentDirectory, parse.Prefix, kind, results);

        return results
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    private static void EnumerateSuggestions(string parent, string prefix, PathKind kind, List<string> results)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(parent))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(NormalizeSeparators(dir) + Path.DirectorySeparatorChar);
            }
        }
        catch
        {
            // ponytail: skip inaccessible entries
        }

        if (kind != PathKind.FileOrDirectory)
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(parent))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(NormalizeSeparators(file));
            }
        }
        catch
        {
            // ponytail: skip inaccessible entries
        }
    }

    private static string NormalizeSeparators(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);
}
