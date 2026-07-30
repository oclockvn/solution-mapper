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
    public void Map_one_legacy_many_upgraded_fans_out()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("LegacyApp/src/Accounts/Accounts.csproj");
        upgraded.AddProject("solutions/api/src/Shared/Accounts/Accounts.csproj");
        upgraded.AddProject("solutions/web/src/Shared/Accounts/Accounts.csproj");
        upgraded.AddProject("solutions/reporting/src/Shared/Accounts/Accounts.csproj");

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);
        var accounts = map.Where(m => m.Name == "Accounts").ToList();

        Assert.Equal(3, accounts.Count);
        Assert.All(accounts, m =>
        {
            Assert.Equal(MappingStatus.Ambiguous, m.Status);
            Assert.True(m.IsOneToMany);
            Assert.True(m.IsAmbiguous);
            Assert.Equal(Path.Combine(legacy.Root, "LegacyApp", "src", "Accounts"), m.LegacyFolder);
            Assert.NotNull(m.UpgradedFolder);
        });
        Assert.Contains(accounts, m => m.UpgradedFolder!.Contains("api"));
        Assert.Contains(accounts, m => m.UpgradedFolder!.Contains("web"));
        Assert.Contains(accounts, m => m.UpgradedFolder!.Contains("reporting"));
        Assert.DoesNotContain(map, m => m.Name == "Accounts" && m.Status == MappingStatus.UpgradedOnly);
    }

    [Fact]
    public void Map_many_legacy_one_upgraded_fans_out()
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
        var foos = map.Where(m => m.ProjectFileName.Equals("Foo.csproj", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Equal(2, foos.Count);
        Assert.All(foos, m =>
        {
            Assert.True(m.IsOneToMany);
            Assert.Equal(MappingStatus.Ambiguous, m.Status);
            Assert.Equal(Path.Combine(upgraded.Root, "src", "Foo"), m.UpgradedFolder);
        });
        Assert.DoesNotContain(map, m => m.Status == MappingStatus.LegacyOnly && m.Name == "Foo");
    }

    [Fact]
    public void Map_many_to_many_still_scores_greedy_pairs()
    {
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("A/Foo/Foo.csproj", TempSolutionTree.MinimalCsproj("FooA", "net10.0", "FooA"));
        legacy.AddProject("B/Foo/Foo.csproj", TempSolutionTree.MinimalCsproj("FooB", "net10.0", "FooB"));
        upgraded.AddProject("X/Foo/Foo.csproj", TempSolutionTree.MinimalCsproj("FooA", "net10.0", "FooA"));
        upgraded.AddProject("Y/Foo/Foo.csproj", TempSolutionTree.MinimalCsproj("FooB", "net10.0", "FooB"));

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);
        var foos = map.Where(m => m.Name == "Foo").ToList();

        Assert.Equal(2, foos.Count);
        Assert.All(foos, m =>
        {
            Assert.False(m.IsOneToMany);
            Assert.Equal(MappingStatus.Ambiguous, m.Status);
        });
        Assert.Contains(foos, m => m.LegacyFolder!.Contains($"{Path.DirectorySeparatorChar}A{Path.DirectorySeparatorChar}")
            && m.UpgradedFolder!.Contains($"{Path.DirectorySeparatorChar}X{Path.DirectorySeparatorChar}"));
        Assert.Contains(foos, m => m.LegacyFolder!.Contains($"{Path.DirectorySeparatorChar}B{Path.DirectorySeparatorChar}")
            && m.UpgradedFolder!.Contains($"{Path.DirectorySeparatorChar}Y{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void Map_duplicate_filenames_picks_strongest_and_marks_ambiguous()
    {
        // kept name: now covered by Map_many_legacy_one_upgraded_fans_out + Map_many_to_many
        using var legacy = TempSolutionTree.Create();
        using var upgraded = TempSolutionTree.Create();
        legacy.AddProject("Services/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo", "net10.0", "Foo"));
        legacy.AddProject("Tests/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo.Tests", "net10.0", "Foo.Tests"));
        upgraded.AddProject("src/Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("Foo", "net10.0", "Foo"));

        var map = ProjectMapper.Map(legacy.Root, upgraded.Root);

        var matched = map.Where(m => m.ProjectFileName.Equals("Foo.csproj", StringComparison.OrdinalIgnoreCase)
            && m.Status is MappingStatus.Matched or MappingStatus.Ambiguous).ToList();
        Assert.Equal(2, matched.Count);
        Assert.All(matched, m => Assert.True(m.IsOneToMany));
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
