using SolutionMapper.Mapping;
using SolutionMapper.Tests.TestHelpers;

namespace SolutionMapper.Tests.Mapping;

public class ProjectMapperTests
{
    [Fact]
    public void Map_one_to_one_different_nesting()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Old/Services/Billing/Billing.csproj");
        upgraded.AddProject("src/Billing/Billing.csproj");

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);

        var billing = Assert.Single(map, m => m.Name == "Billing");
        Assert.Equal(MappingStatus.Matched, billing.Status);
        Assert.False(billing.IsAmbiguous);
        Assert.Equal(1.0, billing.MatchConfidence);
        Assert.Equal(Path.Combine(legacy.Root, "Old", "Services", "Billing"), billing.LegacyFolder);
        Assert.Equal(Path.Combine(upgraded.Root, "src", "Billing"), billing.UpgradedFolder);
    }

    [Fact]
    public void Map_legacy_only_and_upgraded_only()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Bar/Bar.csproj");
        upgraded.AddProject("Baz/Baz.csproj");

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);

        Assert.Contains(map, m => m.Name == "Bar" && m.Status == MappingStatus.LegacyOnly);
        Assert.Contains(map, m => m.Name == "Baz" && m.Status == MappingStatus.UpgradedOnly);
    }

    [Fact]
    public void Map_duplicate_filenames_picks_strongest_and_marks_ambiguous()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Services/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo", "net10.0", "Foo"));
        legacy.AddProject("Tests/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo.Tests", "net10.0", "Foo.Tests"));
        upgraded.AddProject("src/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo", "net10.0", "Foo"));

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);

        var matched = Assert.Single(map, m => m.ProjectFileName.Equals("Foo.csproj", StringComparison.OrdinalIgnoreCase)
            && m.Status is MappingStatus.Matched or MappingStatus.Ambiguous);
        Assert.True(matched.IsAmbiguous);
        Assert.Equal(MappingStatus.Ambiguous, matched.Status);
        Assert.Contains("Services", matched.LegacyFolder!);
        Assert.Contains(map, m => m.Status == MappingStatus.LegacyOnly && m.LegacyFolder!.Contains("Tests"));
    }

    [Fact]
    public void Map_case_insensitive_filenames()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("a/Billing.csproj");
        upgraded.AddProject("b/billing.csproj");

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);
        Assert.Contains(map, m => m.Status == MappingStatus.Matched);
    }

    [Fact]
    public void Map_continues_when_csproj_corrupt()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Good/Good.csproj");
        legacy.AddProject("Bad/Bad.csproj", "<<<");
        upgraded.AddProject("Good/Good.csproj");
        upgraded.AddProject("Bad/Bad.csproj", "<<<");

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);
        Assert.Contains(map, m => m.Name == "Good" && m.Status == MappingStatus.Matched);
    }
}
