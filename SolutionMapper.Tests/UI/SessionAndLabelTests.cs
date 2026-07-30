using SolutionMapper.Mapping;
using SolutionMapper.UI;

namespace SolutionMapper.Tests.UI;

public class LastRootsStoreTests
{
    [Fact]
    public void Save_then_TryLoad_round_trips()
    {
        using var dirs = new DualTempDirs();
        var store = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"), "last-roots.json");

        LastRootsStore.Save(dirs.Legacy, dirs.Upgraded, store);
        var loaded = LastRootsStore.TryLoad(store);

        Assert.NotNull(loaded);
        Assert.Equal(Path.GetFullPath(dirs.Legacy), loaded!.LegacyRoot);
        Assert.Equal(Path.GetFullPath(dirs.Upgraded), loaded.UpgradedRoot);
    }

    [Fact]
    public void TryLoad_missing_file_returns_null()
    {
        var missing = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"), "nope.json");
        Assert.Null(LastRootsStore.TryLoad(missing));
    }

    [Fact]
    public void TryLoad_stale_directory_returns_null()
    {
        using var dirs = new DualTempDirs();
        var store = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"), "last-roots.json");
        LastRootsStore.Save(dirs.Legacy, dirs.Upgraded, store);
        Directory.Delete(dirs.Upgraded, true);

        Assert.Null(LastRootsStore.TryLoad(store));
    }
}

public class ProjectLabelFormatterTests
{
    [Fact]
    public void Format_matched_shows_both_relative_paths_without_status()
    {
        var legacyRoot = @"C:\Legacy";
        var upgradedRoot = @"C:\Net10";
        var m = new ProjectMapping
        {
            Name = "Billing",
            ProjectFileName = "Billing.csproj",
            LegacyFolder = Path.Combine(legacyRoot, "Services", "Billing"),
            UpgradedFolder = Path.Combine(upgradedRoot, "src", "Billing"),
            Status = MappingStatus.Matched,
            MatchConfidence = 1,
            IsAmbiguous = false
        };

        var label = ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot);

        Assert.Contains("Billing", label);
        Assert.DoesNotContain("ambiguous", label);
        Assert.Contains(@"L:Services\Billing", label);
        Assert.Contains(@"U:src\Billing", label);
        Assert.DoesNotContain("[", label);
        Assert.DoesNotContain("]", label);
    }

    [Fact]
    public void Format_upgraded_only_includes_status_and_U_path()
    {
        var legacyRoot = @"C:\Legacy";
        var upgradedRoot = @"C:\Net10";
        var m = new ProjectMapping
        {
            Name = "Accounts",
            ProjectFileName = "Accounts.csproj",
            UpgradedFolder = Path.Combine(upgradedRoot, "src", "Foo", "Accounts"),
            Status = MappingStatus.UpgradedOnly,
            MatchConfidence = 0,
            IsAmbiguous = false
        };

        var label = ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot);

        Assert.Contains("Accounts  upgraded only", label);
        Assert.Contains(@"U:src\Foo\Accounts", label);
        Assert.DoesNotContain("L:", label);
    }

    [Fact]
    public void Format_ambiguous_includes_status_and_both_paths()
    {
        var legacyRoot = @"C:\Legacy";
        var upgradedRoot = @"C:\Net10";
        var m = new ProjectMapping
        {
            Name = "Accounts",
            ProjectFileName = "Accounts.csproj",
            LegacyFolder = Path.Combine(legacyRoot, "Services", "Accounts"),
            UpgradedFolder = Path.Combine(upgradedRoot, "src", "Accounts"),
            Status = MappingStatus.Ambiguous,
            MatchConfidence = 0.8,
            IsAmbiguous = true
        };

        var label = ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot);

        Assert.Contains("(ambiguous)", label);
        Assert.Contains("L:", label);
        Assert.Contains("U:", label);
    }

    [Fact]
    public void MatchesFilter_matches_relative_path_segment()
    {
        var legacyRoot = @"C:\Legacy";
        var upgradedRoot = @"C:\Net10";
        var m = new ProjectMapping
        {
            Name = "Accounts",
            ProjectFileName = "Accounts.csproj",
            UpgradedFolder = Path.Combine(upgradedRoot, "src", "Foo", "Accounts"),
            Status = MappingStatus.UpgradedOnly
        };

        Assert.True(ProjectLabelFormatter.MatchesFilter(m, @"src\Foo", legacyRoot, upgradedRoot));
        Assert.False(ProjectLabelFormatter.MatchesFilter(m, "Nope", legacyRoot, upgradedRoot));
    }

    [Fact]
    public void Format_one_to_many_shows_arrow_label()
    {
        var legacyRoot = @"C:\Legacy";
        var upgradedRoot = @"C:\Net10";
        var m = new ProjectMapping
        {
            Name = "Accounts",
            ProjectFileName = "Accounts.csproj",
            LegacyFolder = Path.Combine(legacyRoot, "src", "Accounts"),
            UpgradedFolder = Path.Combine(upgradedRoot, "api", "Accounts"),
            Status = MappingStatus.Ambiguous,
            IsAmbiguous = true,
            IsOneToMany = true
        };

        var label = ProjectLabelFormatter.Format(m, legacyRoot, upgradedRoot);

        Assert.Contains("(1→N)", label);
        Assert.DoesNotContain("ambiguous", label);
        Assert.Contains("L:", label);
        Assert.Contains("U:", label);
    }
}

file sealed class DualTempDirs : IDisposable
{
    public string Legacy { get; } = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"), "legacy");
    public string Upgraded { get; } = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"), "upgraded");

    public DualTempDirs()
    {
        Directory.CreateDirectory(Legacy);
        Directory.CreateDirectory(Upgraded);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Legacy)) Directory.Delete(Legacy, true); } catch { /* ponytail: best-effort */ }
        try { if (Directory.Exists(Upgraded)) Directory.Delete(Upgraded, true); } catch { /* ponytail: best-effort */ }
    }
}
