using SolutionMapper.UI;

namespace SolutionMapper.Tests.UI;

public class PathCompletionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_empty_or_whitespace_suggests_drives(string buffer)
    {
        var result = PathCompletion.Parse(buffer);

        Assert.True(result.SuggestDrives);
        Assert.Null(result.ParentDirectory);
        Assert.Equal("", result.Prefix);
    }

    [Fact]
    public void Parse_drive_root_with_trailing_separator()
    {
        if (!OperatingSystem.IsWindows())
            return; // ponytail: drive-letter paths are Windows-only

        var result = PathCompletion.Parse(@"D:\");

        Assert.False(result.SuggestDrives);
        Assert.Equal(@"D:\", result.ParentDirectory);
        Assert.Equal("", result.Prefix);
    }

    [Fact]
    public void Parse_drive_without_trailing_separator()
    {
        if (!OperatingSystem.IsWindows())
            return; // ponytail: drive-letter paths are Windows-only

        var result = PathCompletion.Parse("D:");

        Assert.False(result.SuggestDrives);
        Assert.Equal(@"D:\", result.ParentDirectory);
        Assert.Equal("", result.Prefix);
    }

    [Fact]
    public void Parse_partial_segment_on_drive()
    {
        if (!OperatingSystem.IsWindows())
            return; // ponytail: drive-letter paths are Windows-only

        var result = PathCompletion.Parse(@"D:\p");

        Assert.False(result.SuggestDrives);
        Assert.Equal(@"D:\", result.ParentDirectory);
        Assert.Equal("p", result.Prefix);
    }

    [Fact]
    public void Parse_directory_with_trailing_separator()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root;

        var result = PathCompletion.Parse(buffer);

        Assert.False(result.SuggestDrives);
        Assert.Equal(fixture.Root, result.ParentDirectory);
        Assert.Equal("", result.Prefix);
    }

    [Fact]
    public void Parse_directory_with_forward_slash_trailing_separator()
    {
        using var fixture = new PathCompletionFixture();
        var mixed = fixture.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/') + "/";

        var result = PathCompletion.Parse(mixed);

        Assert.False(result.SuggestDrives);
        Assert.Equal(fixture.Root, result.ParentDirectory);
        Assert.Equal("", result.Prefix);
    }

    [Fact]
    public void Parse_partial_segment_under_directory()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root + "p";

        var result = PathCompletion.Parse(buffer);

        Assert.False(result.SuggestDrives);
        Assert.Equal(
            fixture.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            result.ParentDirectory);
        Assert.Equal("p", result.Prefix);
    }

    [Fact]
    public void GetSuggestions_empty_buffer_returns_logical_drives()
    {
        var suggestions = PathCompletion.GetSuggestions("", PathKind.Directory);
        var expected = Environment.GetLogicalDrives()
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Take(PathCompletion.DefaultMax)
            .ToList();

        Assert.Equal(expected, suggestions);
    }

    [Fact]
    public void GetSuggestions_prefix_filters_directories()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root + "p";

        var suggestions = PathCompletion.GetSuggestions(buffer, PathKind.Directory);

        Assert.Single(suggestions);
        Assert.Equal(fixture.ProjectsDir, suggestions[0]);
    }

    [Fact]
    public void GetSuggestions_directory_mode_excludes_files()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root + "readme";

        var dirSuggestions = PathCompletion.GetSuggestions(buffer, PathKind.Directory);
        var allSuggestions = PathCompletion.GetSuggestions(buffer, PathKind.FileOrDirectory);

        Assert.Empty(dirSuggestions);
        Assert.Single(allSuggestions);
        Assert.Equal(fixture.ReadmePath, allSuggestions[0]);
    }

    [Fact]
    public void GetSuggestions_file_or_directory_includes_files_and_folders()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root + "r";

        var suggestions = PathCompletion.GetSuggestions(buffer, PathKind.FileOrDirectory);

        Assert.Equal(2, suggestions.Count);
        Assert.Contains(fixture.ReadmePath, suggestions);
        Assert.Contains(fixture.ReportDir, suggestions);
    }

    [Fact]
    public void GetSuggestions_respects_max_cap()
    {
        using var fixture = new PathCompletionFixture();
        var buffer = fixture.Root;

        var suggestions = PathCompletion.GetSuggestions(buffer, PathKind.Directory, max: 2);

        Assert.Equal(2, suggestions.Count);
    }
}

file sealed class PathCompletionFixture : IDisposable
{
    public string Root { get; }
    public string ProjectsDir { get; }
    public string ReadmePath { get; }
    public string ReportDir { get; }

    public PathCompletionFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"))
            + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(Path.Combine(Root, "alpha"));
        Directory.CreateDirectory(Path.Combine(Root, "beta"));
        Directory.CreateDirectory(Path.Combine(Root, "report"));
        var projects = Path.Combine(Root, "projects");
        Directory.CreateDirectory(projects);
        ProjectsDir = projects + Path.DirectorySeparatorChar;
        ReadmePath = Path.Combine(Root, "readme.txt");
        File.WriteAllText(ReadmePath, "test");
        ReportDir = Path.Combine(Root, "report") + Path.DirectorySeparatorChar;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root.TrimEnd(Path.DirectorySeparatorChar))) Directory.Delete(Root.TrimEnd(Path.DirectorySeparatorChar), true); }
        catch { /* ponytail: best-effort */ }
    }
}
