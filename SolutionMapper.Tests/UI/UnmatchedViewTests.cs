using SolutionMapper.Mapping;
using SolutionMapper.UI;

namespace SolutionMapper.Tests.UI;

public class UnmatchedViewTests
{
    static readonly string LegacyRoot = Path.Combine(Path.GetTempPath(), "Legacy");
    static readonly string UpgradedRoot = Path.Combine(Path.GetTempPath(), "Net10");

    static ProjectMapping LegacyOnly(string relCsproj) => new()
    {
        Name = Path.GetFileNameWithoutExtension(relCsproj),
        ProjectFileName = Path.GetFileName(relCsproj),
        LegacyProjectFile = Path.Combine(LegacyRoot, relCsproj.Replace('/', Path.DirectorySeparatorChar)),
        Status = MappingStatus.LegacyOnly
    };

    static ProjectMapping UpgradedOnly(string relCsproj) => new()
    {
        Name = Path.GetFileNameWithoutExtension(relCsproj),
        ProjectFileName = Path.GetFileName(relCsproj),
        UpgradedProjectFile = Path.Combine(UpgradedRoot, relCsproj.Replace('/', Path.DirectorySeparatorChar)),
        Status = MappingStatus.UpgradedOnly
    };

    static ProjectMapping Matched() => new()
    {
        Name = "Billing",
        ProjectFileName = "Billing.csproj",
        LegacyProjectFile = Path.Combine(LegacyRoot, "a", "Billing.csproj"),
        UpgradedProjectFile = Path.Combine(UpgradedRoot, "b", "Billing.csproj"),
        Status = MappingStatus.Matched
    };

    [Fact]
    public void Lists_each_side_under_its_header_as_relative_csproj_paths()
    {
        var mappings = new[]
        {
            Matched(),
            LegacyOnly("TallyMain/test/Addresses.Tests/Addresses.Tests.csproj"),
            UpgradedOnly("solutions/api/Shared/Foo.references/Foo.references.csproj"),
        };

        var lines = UnmatchedView.BuildLines(mappings, LegacyRoot, UpgradedRoot);
        var text = string.Join("\n", lines);

        Assert.Contains("LEGACY ONLY (1) — in legacy, no match in upgraded", text);
        Assert.Contains("UPGRADED ONLY (1) — in upgraded, no match in legacy", text);
        Assert.Contains("  " + Path.Combine("TallyMain", "test", "Addresses.Tests", "Addresses.Tests.csproj"), lines);
        Assert.Contains("  " + Path.Combine("solutions", "api", "Shared", "Foo.references", "Foo.references.csproj"), lines);
        // matched projects never appear
        Assert.DoesNotContain(lines, l => l.Contains("Billing"));
    }

    [Fact]
    public void Legacy_section_is_sorted_by_path_so_subtrees_group_naturally()
    {
        var mappings = new[]
        {
            LegacyOnly("TallyMain/test/Zeta.csproj"),
            LegacyOnly("BillingV2/App/App.csproj"),
            LegacyOnly("TallyMain/test/Alpha.csproj"),
        };

        var lines = UnmatchedView.BuildLines(mappings, LegacyRoot, UpgradedRoot).ToList();
        var body = lines
            .SkipWhile(l => !l.StartsWith("LEGACY ONLY", StringComparison.Ordinal))
            .Skip(2)
            .TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(new[]
        {
            "  " + Path.Combine("BillingV2", "App", "App.csproj"),
            "  " + Path.Combine("TallyMain", "test", "Alpha.csproj"),
            "  " + Path.Combine("TallyMain", "test", "Zeta.csproj"),
        }, body);
    }

    [Fact]
    public void Empty_section_renders_none_placeholder()
    {
        var mappings = new[] { LegacyOnly("Only/Legacy.csproj") };

        var lines = UnmatchedView.BuildLines(mappings, LegacyRoot, UpgradedRoot).ToList();

        var upgradedIdx = lines.FindIndex(l => l.StartsWith("UPGRADED ONLY", StringComparison.Ordinal));
        Assert.Equal("UPGRADED ONLY (0) — in upgraded, no match in legacy", lines[upgradedIdx]);
        Assert.Equal("  (none)", lines[upgradedIdx + 2]);
    }

    [Fact]
    public void Duplicate_names_are_kept_as_separate_lines_disambiguated_by_path()
    {
        var mappings = new[]
        {
            UpgradedOnly("solutions/api/Shared/Foo.references/Foo.references.csproj"),
            UpgradedOnly("solutions/web/Shared/Foo.references/Foo.references.csproj"),
        };

        var lines = UnmatchedView.BuildLines(mappings, LegacyRoot, UpgradedRoot);

        Assert.Contains("  " + Path.Combine("solutions", "api", "Shared", "Foo.references", "Foo.references.csproj"), lines);
        Assert.Contains("  " + Path.Combine("solutions", "web", "Shared", "Foo.references", "Foo.references.csproj"), lines);
        Assert.Contains("UPGRADED ONLY (2) — in upgraded, no match in legacy", string.Join("\n", lines));
    }
}

public class MappingSummaryTests
{
    [Fact]
    public void UnmatchedCount_counts_only_legacy_and_upgraded_only()
    {
        var mappings = new[]
        {
            new ProjectMapping { Name = "a", ProjectFileName = "a.csproj", Status = MappingStatus.Matched },
            new ProjectMapping { Name = "b", ProjectFileName = "b.csproj", Status = MappingStatus.LegacyOnly },
            new ProjectMapping { Name = "c", ProjectFileName = "c.csproj", Status = MappingStatus.UpgradedOnly },
            new ProjectMapping { Name = "d", ProjectFileName = "d.csproj", Status = MappingStatus.Ambiguous, IsOneToMany = true },
        };

        Assert.Equal(2, MappingSummary.UnmatchedCount(mappings));
    }
}
