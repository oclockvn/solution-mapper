# Solution Mapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `mapper.exe` that maps legacy vs upgraded .NET solution project folders and opens selected pairs in WinMerge / Beyond Compare / VS Code.

**Architecture:** Single .NET 10 console app (`AssemblyName=mapper`) with `Mapping/`, `UI/`, `DiffTools/` folders plus xUnit test project. Mapping produces folder pairs; diff adapters only consume paths.

**Tech Stack:** .NET 10, C#, Spectre.Console, System.Xml.Linq, System.Text.Json, xUnit

## Global Constraints

- Runtime: .NET 10 (`net10.0`)
- Project name: `SolutionMapper`; exe name: `mapper` (`<AssemblyName>mapper</AssemblyName>`)
- Dependencies: Spectre.Console only (app); xUnit (tests). No MSBuild packages.
- Diff tools in MVP: WinMerge, Beyond Compare, VS Code. No Custom tool.
- Mapping must not reference diff tools; diff tools must not reference mapping types.
- Read-only vs solution trees (temp empty dir + optional JSON export only writes).
- Shell commands: prefix with `rtk` (e.g. `rtk dotnet test`).
- Prefer `dotnet` CLI for sln/csproj/package/reference changes.
- Git: folder may not be a repo — skip Commit steps unless `.git` exists (do not `git init` unless user asks).

## File structure

| Path | Responsibility |
|---|---|
| `SolutionMapper/Program.cs` | Parse args, orchestrate flow, exit codes |
| `SolutionMapper/Mapping/ProjectMapping.cs` | Model + `MappingStatus` |
| `SolutionMapper/Mapping/ProjectDiscovery.cs` | Find `*.csproj` under a root |
| `SolutionMapper/Mapping/ProjectMetadata.cs` | Lightweight metadata DTO |
| `SolutionMapper/Mapping/ProjectMetadataReader.cs` | `XDocument` property extraction |
| `SolutionMapper/Mapping/ProjectMatcher.cs` | Scoring + pairing |
| `SolutionMapper/Mapping/ProjectMapper.cs` | Orchestrate discover → match |
| `SolutionMapper/Mapping/MappingExport.cs` | JSON export |
| `SolutionMapper/UI/MappingSummary.cs` | Print summary counts |
| `SolutionMapper/UI/ProjectPicker.cs` | Spectre search + multi-select |
| `SolutionMapper/UI/DiffToolPicker.cs` | Pick available tool |
| `SolutionMapper/DiffTools/IDiffTool.cs` | Diff tool contract |
| `SolutionMapper/DiffTools/DiffToolDiscovery.cs` | Built-in tool list + available filter |
| `SolutionMapper/DiffTools/WinMergeDiffTool.cs` | WinMerge adapter |
| `SolutionMapper/DiffTools/BeyondCompareDiffTool.cs` | BC adapter |
| `SolutionMapper/DiffTools/VsCodeDiffTool.cs` | VS Code adapter |
| `SolutionMapper/DiffTools/EmptyFolder.cs` | `%TEMP%\SolutionMapper\empty` |
| `SolutionMapper.Tests/Mapping/*` | Mapping fixture tests |

---

### Task 1: Scaffold solution

**Files:**
- Create: `SolutionMapper.sln`, `SolutionMapper/SolutionMapper.csproj`, `SolutionMapper.Tests/SolutionMapper.Tests.csproj`
- Modify: `SolutionMapper/SolutionMapper.csproj` (AssemblyName, Spectre)
- Delete: default `Class1.cs` / template fluff if present

**Interfaces:**
- Consumes: none
- Produces: buildable `mapper` console + test project referencing it

- [ ] **Step 1: Create solution and projects via CLI**

```powershell
cd /path/to/diff-mapper
rtk dotnet new sln -n SolutionMapper
rtk dotnet new console -n SolutionMapper -o SolutionMapper -f net10.0
rtk dotnet new xunit -n SolutionMapper.Tests -o SolutionMapper.Tests -f net10.0
rtk dotnet sln SolutionMapper.sln add SolutionMapper/SolutionMapper.csproj SolutionMapper.Tests/SolutionMapper.Tests.csproj
rtk dotnet add SolutionMapper.Tests/SolutionMapper.Tests.csproj reference SolutionMapper/SolutionMapper.csproj
rtk dotnet add SolutionMapper/SolutionMapper.csproj package Spectre.Console
```

- [ ] **Step 2: Set AssemblyName to mapper**

In `SolutionMapper/SolutionMapper.csproj`, ensure:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <AssemblyName>mapper</AssemblyName>
  <RootNamespace>SolutionMapper</RootNamespace>
</PropertyGroup>
```

- [ ] **Step 3: Build**

Run: `rtk dotnet build SolutionMapper.sln`  
Expected: Build succeeded.

- [ ] **Step 4: Commit** (skip if no git)

```powershell
rtk git add SolutionMapper.sln SolutionMapper SolutionMapper.Tests
rtk git commit -m "chore: scaffold SolutionMapper console and test projects"
```

---

### Task 2: Mapping model + discovery

**Files:**
- Create: `SolutionMapper/Mapping/ProjectMapping.cs`
- Create: `SolutionMapper/Mapping/ProjectDiscovery.cs`
- Create: `SolutionMapper.Tests/Mapping/ProjectDiscoveryTests.cs`
- Create: `SolutionMapper.Tests/TestHelpers/TempSolutionTree.cs`

**Interfaces:**
- Consumes: none
- Produces:
  - `enum MappingStatus { Matched, LegacyOnly, UpgradedOnly, Ambiguous }`
  - `record ProjectMapping { Name, ProjectFileName, LegacyProjectFile?, UpgradedProjectFile?, LegacyFolder?, UpgradedFolder?, Status, MatchConfidence, IsAmbiguous }`
  - `static class ProjectDiscovery` with `IReadOnlyList<string> FindProjects(string root)` and `void EnsureHasProjects(string root, string label)` throwing `InvalidOperationException` with clear message if none / root missing

- [ ] **Step 1: Write failing discovery test**

```csharp
public class ProjectDiscoveryTests
{
    [Fact]
    public void FindProjects_recurses_and_finds_csproj()
    {
        using var tree = TempSolutionTree.Create();
        tree.AddProject("Services/Billing/Billing.csproj");
        tree.AddProject("src/deep/Customer/Customer.csproj");

        var found = ProjectDiscovery.FindProjects(tree.Root);

        Assert.Equal(2, found.Count);
        Assert.Contains(found, p => p.EndsWith("Billing.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(found, p => p.EndsWith("Customer.csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureHasProjects_throws_when_empty()
    {
        using var tree = TempSolutionTree.Create();
        var ex = Assert.Throws<InvalidOperationException>(
            () => ProjectDiscovery.EnsureHasProjects(tree.Root, "Legacy"));
        Assert.Contains(tree.Root, ex.Message);
    }
}
```

Helper:

```csharp
public sealed class TempSolutionTree : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SolutionMapperTests", Guid.NewGuid().ToString("N"));

    public static TempSolutionTree Create()
    {
        var t = new TempSolutionTree();
        Directory.CreateDirectory(t.Root);
        return t;
    }

    public string AddProject(string relativePath, string? contents = null)
    {
        var full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents ?? MinimalCsproj());
        return full;
    }

    public static string MinimalCsproj(
        string? assemblyName = null,
        string? targetFramework = "net10.0",
        string? rootNamespace = null,
        IEnumerable<string>? projectRefs = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("""<Project Sdk="Microsoft.NET.Sdk">""");
        sb.AppendLine("<PropertyGroup>");
        if (assemblyName is not null) sb.AppendLine($"<AssemblyName>{assemblyName}</AssemblyName>");
        if (targetFramework is not null) sb.AppendLine($"<TargetFramework>{targetFramework}</TargetFramework>");
        if (rootNamespace is not null) sb.AppendLine($"<RootNamespace>{rootNamespace}</RootNamespace>");
        sb.AppendLine("</PropertyGroup>");
        if (projectRefs is not null)
        {
            sb.AppendLine("<ItemGroup>");
            foreach (var r in projectRefs)
                sb.AppendLine($"""<ProjectReference Include="{r}" />""");
            sb.AppendLine("</ItemGroup>");
        }
        sb.AppendLine("</Project>");
        return sb.ToString();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { /* ponytail: best-effort temp cleanup */ }
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `rtk dotnet test SolutionMapper.Tests --filter ProjectDiscoveryTests`  
Expected: FAIL (types missing).

- [ ] **Step 3: Implement model + discovery**

`ProjectMapping.cs`:

```csharp
namespace SolutionMapper.Mapping;

public enum MappingStatus
{
    Matched,
    LegacyOnly,
    UpgradedOnly,
    Ambiguous
}

public sealed record ProjectMapping
{
    public required string Name { get; init; }
    public required string ProjectFileName { get; init; }
    public string? LegacyProjectFile { get; init; }
    public string? UpgradedProjectFile { get; init; }
    public string? LegacyFolder { get; init; }
    public string? UpgradedFolder { get; init; }
    public MappingStatus Status { get; init; }
    public double MatchConfidence { get; init; }
    public bool IsAmbiguous { get; init; }
}
```

`ProjectDiscovery.cs`:

```csharp
namespace SolutionMapper.Mapping;

public static class ProjectDiscovery
{
    public static IReadOnlyList<string> FindProjects(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Error: solution root does not exist.\n\n  {root}");

        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).ToList();
    }

    public static void EnsureHasProjects(string root, string label)
    {
        var projects = FindProjects(root);
        if (projects.Count == 0)
            throw new InvalidOperationException($"Error: No .csproj files found under:\n\n  {root}");
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `rtk dotnet test SolutionMapper.Tests --filter ProjectDiscoveryTests`  
Expected: PASS

- [ ] **Step 5: Commit** (skip if no git)

```powershell
rtk git add SolutionMapper/Mapping SolutionMapper.Tests
rtk git commit -m "feat: add project mapping model and csproj discovery"
```

---

### Task 3: Metadata reader

**Files:**
- Create: `SolutionMapper/Mapping/ProjectMetadata.cs`
- Create: `SolutionMapper/Mapping/ProjectMetadataReader.cs`
- Create: `SolutionMapper.Tests/Mapping/ProjectMetadataReaderTests.cs`

**Interfaces:**
- Consumes: `TempSolutionTree.MinimalCsproj`
- Produces:
  - `record ProjectMetadata(string ProjectFile, string? AssemblyName, string? TargetFramework, string? RootNamespace, IReadOnlyList<string> ProjectReferenceNames)`
  - `static ProjectMetadata? ProjectMetadataReader.TryRead(string projectFile)` — returns null on unreadable XML (no throw)

- [ ] **Step 1: Write failing tests**

```csharp
public class ProjectMetadataReaderTests
{
    [Fact]
    public void TryRead_extracts_properties_and_refs()
    {
        using var tree = TempSolutionTree.Create();
        var path = tree.AddProject(
            "Foo/Foo.csproj",
            TempSolutionTree.MinimalCsproj("FooAsm", "net10.0", "Foo.Root", ["../Bar/Bar.csproj"]));

        var meta = ProjectMetadataReader.TryRead(path);

        Assert.NotNull(meta);
        Assert.Equal("FooAsm", meta!.AssemblyName);
        Assert.Equal("net10.0", meta.TargetFramework);
        Assert.Equal("Foo.Root", meta.RootNamespace);
        Assert.Contains("Bar", meta.ProjectReferenceNames);
    }

    [Fact]
    public void TryRead_returns_null_on_corrupt_xml()
    {
        using var tree = TempSolutionTree.Create();
        var path = tree.AddProject("Bad/Bad.csproj", "not xml <<<");
        Assert.Null(ProjectMetadataReader.TryRead(path));
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `rtk dotnet test SolutionMapper.Tests --filter ProjectMetadataReaderTests`

- [ ] **Step 3: Implement**

```csharp
namespace SolutionMapper.Mapping;

public sealed record ProjectMetadata(
    string ProjectFile,
    string? AssemblyName,
    string? TargetFramework,
    string? RootNamespace,
    IReadOnlyList<string> ProjectReferenceNames);

public static class ProjectMetadataReader
{
    public static ProjectMetadata? TryRead(string projectFile)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Load(projectFile);
            // ponytail: ignore MSBuild conditions/namespaces; static props only
            string? Prop(string name) =>
                doc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();

            var assembly = Prop("AssemblyName");
            if (assembly is not null && assembly.Contains("$(")) assembly = null;

            var tfm = Prop("TargetFramework") ?? Prop("TargetFrameworks");
            var rootNs = Prop("RootNamespace");
            if (rootNs is not null && rootNs.Contains("$(")) rootNs = null;

            var refs = doc.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            return new ProjectMetadata(projectFile, assembly, tfm, rootNs, refs);
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit** (skip if no git)

```powershell
rtk git commit -am "feat: read lightweight csproj metadata via XDocument"
```

---

### Task 4: Matcher + mapper (core)

**Files:**
- Create: `SolutionMapper/Mapping/ProjectMatcher.cs`
- Create: `SolutionMapper/Mapping/ProjectMapper.cs`
- Create: `SolutionMapper.Tests/Mapping/ProjectMapperTests.cs`

**Interfaces:**
- Consumes: `ProjectDiscovery`, `ProjectMetadataReader`, `ProjectMapping`
- Produces:
  - `static IReadOnlyList<ProjectMapping> ProjectMapper.Map(string legacyRoot, string upgradedRoot)`
  - Scoring: filename +100, AssemblyName +50, TFM +10, RootNamespace +5, shared ProjectReference name +10
  - One-to-one → `Matched` / confidence 1.0 / not ambiguous
  - Dup auto-resolve → `Ambiguous` / `IsAmbiguous=true`
  - One side only → `LegacyOnly` / `UpgradedOnly`
  - Filename compare: `StringComparer.OrdinalIgnoreCase`

- [ ] **Step 1: Write failing mapper tests**

```csharp
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
```

- [ ] **Step 2: Run — expect FAIL**

Run: `rtk dotnet test SolutionMapper.Tests --filter ProjectMapperTests`

- [ ] **Step 3: Implement matcher + mapper**

`ProjectMatcher.cs` — score two metadata objects (filename already matched):

```csharp
namespace SolutionMapper.Mapping;

public static class ProjectMatcher
{
    public static int Score(ProjectMetadata? left, ProjectMetadata? right, string fileName)
    {
        var score = 100; // same filename group
        if (left is null || right is null) return score;

        if (Eq(left.AssemblyName, right.AssemblyName)) score += 50;
        if (Eq(left.TargetFramework, right.TargetFramework)) score += 10;
        if (Eq(left.RootNamespace, right.RootNamespace)) score += 5;

        var overlap = left.ProjectReferenceNames.Intersect(
            right.ProjectReferenceNames, StringComparer.OrdinalIgnoreCase).Any();
        if (overlap) score += 10;

        return score;

        static bool Eq(string? a, string? b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
            && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
```

`ProjectMapper.cs`:

```csharp
namespace SolutionMapper.Mapping;

public static class ProjectMapper
{
    public static IReadOnlyList<ProjectMapping> Map(string legacyRoot, string upgradedRoot)
    {
        var legacy = ProjectDiscovery.FindProjects(legacyRoot);
        var upgraded = ProjectDiscovery.FindProjects(upgradedRoot);

        var legacyByName = legacy.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var upgradedByName = upgraded.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var names = legacyByName.Keys.Union(upgradedByName.Keys, StringComparer.OrdinalIgnoreCase);
        var results = new List<ProjectMapping>();

        foreach (var fileName in names)
        {
            legacyByName.TryGetValue(fileName, out var leftList);
            upgradedByName.TryGetValue(fileName, out var rightList);
            leftList ??= [];
            rightList ??= [];

            if (leftList.Count == 1 && rightList.Count == 1)
            {
                results.Add(Create(fileName, leftList[0], rightList[0], MappingStatus.Matched, 1.0, false));
                continue;
            }

            if (leftList.Count == 0)
            {
                foreach (var r in rightList)
                    results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false));
                continue;
            }

            if (rightList.Count == 0)
            {
                foreach (var l in leftList)
                    results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false));
                continue;
            }

            // duplicates: greedy strongest pairs
            var leftMeta = leftList.Select(p => (Path: p, Meta: ProjectMetadataReader.TryRead(p))).ToList();
            var rightMeta = rightList.Select(p => (Path: p, Meta: ProjectMetadataReader.TryRead(p))).ToList();
            var usedRight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedLeft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var pairs = new List<(string L, string R, int Score)>();
            foreach (var l in leftMeta)
            foreach (var r in rightMeta)
                pairs.Add((l.Path, r.Path, ProjectMatcher.Score(l.Meta, r.Meta, fileName)));

            foreach (var pair in pairs.OrderByDescending(p => p.Score))
            {
                if (usedLeft.Contains(pair.L) || usedRight.Contains(pair.R)) continue;
                usedLeft.Add(pair.L);
                usedRight.Add(pair.R);
                var confidence = Math.Min(1.0, pair.Score / 175.0);
                results.Add(Create(fileName, pair.L, pair.R, MappingStatus.Ambiguous, confidence, true));
            }

            foreach (var l in leftList.Where(p => !usedLeft.Contains(p)))
                results.Add(Create(fileName, l, null, MappingStatus.LegacyOnly, 0, false));
            foreach (var r in rightList.Where(p => !usedRight.Contains(p)))
                results.Add(Create(fileName, null, r, MappingStatus.UpgradedOnly, 0, false));
        }

        return results
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static ProjectMapping Create(
        string fileName, string? legacyFile, string? upgradedFile,
        MappingStatus status, double confidence, bool ambiguous) => new()
    {
        Name = Path.GetFileNameWithoutExtension(fileName),
        ProjectFileName = fileName,
        LegacyProjectFile = legacyFile,
        UpgradedProjectFile = upgradedFile,
        LegacyFolder = legacyFile is null ? null : Path.GetDirectoryName(legacyFile),
        UpgradedFolder = upgradedFile is null ? null : Path.GetDirectoryName(upgradedFile),
        Status = status,
        MatchConfidence = confidence,
        IsAmbiguous = ambiguous
    };
}
```

- [ ] **Step 4: Run — expect PASS**

Run: `rtk dotnet test SolutionMapper.Tests --filter ProjectMapperTests`  
Expected: all PASS

- [ ] **Step 5: Commit** (skip if no git)

```powershell
rtk git commit -am "feat: map projects by filename with metadata scoring"
```

---

### Task 5: JSON export

**Files:**
- Create: `SolutionMapper/Mapping/MappingExport.cs`
- Create: `SolutionMapper.Tests/Mapping/MappingExportTests.cs`

**Interfaces:**
- Consumes: `ProjectMapping`
- Produces: `static void MappingExport.Write(string path, string legacyRoot, string upgradedRoot, IReadOnlyList<ProjectMapping> projects)` — camelCase JSON with roots + projects array

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void Write_emits_roots_and_projects()
{
    using var dir = TempSolutionTree.Create();
    var outPath = Path.Combine(dir.Root, "mapping.json");
    var projects = new[]
    {
        new ProjectMapping
        {
            Name = "Billing",
            ProjectFileName = "Billing.csproj",
            LegacyFolder = @"C:\L\Billing",
            UpgradedFolder = @"C:\N\Billing",
            Status = MappingStatus.Matched,
            MatchConfidence = 1.0,
            IsAmbiguous = false
        }
    };

    MappingExport.Write(outPath, @"C:\L", @"C:\N", projects);

    using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
    Assert.Equal(@"C:\L", doc.RootElement.GetProperty("legacyRoot").GetString());
    Assert.Equal("Billing", doc.RootElement.GetProperty("projects")[0].GetProperty("name").GetString());
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement**

```csharp
namespace SolutionMapper.Mapping;

public static class MappingExport
{
    public static void Write(
        string path, string legacyRoot, string upgradedRoot, IReadOnlyList<ProjectMapping> projects)
    {
        var payload = new
        {
            legacyRoot,
            upgradedRoot,
            projects
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }
}
```

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit** (skip if no git)

---

### Task 6: Diff tool adapters

**Files:**
- Create: `SolutionMapper/DiffTools/IDiffTool.cs`
- Create: `SolutionMapper/DiffTools/WinMergeDiffTool.cs`
- Create: `SolutionMapper/DiffTools/BeyondCompareDiffTool.cs`
- Create: `SolutionMapper/DiffTools/VsCodeDiffTool.cs`
- Create: `SolutionMapper/DiffTools/DiffToolDiscovery.cs`
- Create: `SolutionMapper/DiffTools/EmptyFolder.cs`
- Create: `SolutionMapper.Tests/DiffTools/DiffToolArgumentTests.cs`

**Interfaces:**
- Consumes: none (paths only)
- Produces:
  - `interface IDiffTool { string Name; bool IsAvailable(); string? FindExecutable(); void Open(string leftFolder, string rightFolder); }`
  - `DiffToolDiscovery.GetBuiltIn()` → WinMerge, Beyond Compare, VS Code
  - `DiffToolDiscovery.GetAvailable()` → where `IsAvailable()`
  - `EmptyFolder.GetPath()` → `%TEMP%\SolutionMapper\empty` (create)
  - WinMerge args include `/r`, `/ul`, `/ur`, `/dl Legacy`, `/dr .NET 10`, then folders
  - Beyond Compare: exe + left + right
  - VS Code: `code --diff` is file-oriented; for folders use `code -n -d left right` or `code --folder-uri` — use `code -d left right` only if acceptable; prefer launching `code` with both folders: `code --new-window left right` is weak. Spec: include if acceptable. Use: `code -n left` is wrong. **Use** `Process` with `code` and ArgumentList `-d`, left, right when both are dirs (VS Code Compare Folders extension not required — actually stock VS Code `-d` is file diff). For MVP folder compare in VS Code: launch `code` with ArgumentList `--diff` does not work for folders. Spec says folder semantics may differ. **Implement** as: `code -n` then open both? Simplest acceptable: `ArgumentList: "-n", leftFolder, rightFolder` opens two folders in one window — document that. Better: use `code` with `-d` only for files. Design said include VS Code. Implement Open as:

```csharp
// VS Code: open both folders in a new window (stock has no first-class folder-diff CLI)
startInfo.ArgumentList.Add("-n");
startInfo.ArgumentList.Add(leftFolder);
startInfo.ArgumentList.Add(rightFolder);
```

- [ ] **Step 1: Write test for WinMerge argument building**

Expose internal-friendly static helpers or test via a package-visible method. Simplest: `public static ProcessStartInfo CreateStartInfo(string exe, string left, string right)` on each tool for testing.

```csharp
[Fact]
public void WinMerge_args_include_recursive_and_labels()
{
    var psi = WinMergeDiffTool.CreateStartInfo(@"C:\WM\WinMergeU.exe", @"C:\L", @"C:\R");
    Assert.Equal("/r", psi.ArgumentList[0]);
    Assert.Contains("Legacy", psi.ArgumentList);
    Assert.Contains(".NET 10", psi.ArgumentList);
    Assert.Equal(@"C:\L", psi.ArgumentList[^2]);
    Assert.Equal(@"C:\R", psi.ArgumentList[^1]);
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement adapters**

`IDiffTool.cs` — as in design.

`WinMergeDiffTool.cs`:

```csharp
namespace SolutionMapper.DiffTools;

public sealed class WinMergeDiffTool : IDiffTool
{
    public string Name => "WinMerge";

    public bool IsAvailable() => FindExecutable() is not null;

    public string? FindExecutable()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinMerge", "WinMergeU.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinMerge", "WinMergeU.exe"),
            "WinMergeU.exe"
        ];
        foreach (var c in candidates)
        {
            if (c is "WinMergeU.exe")
            {
                var onPath = FindOnPath(c);
                if (onPath is not null) return onPath;
            }
            else if (File.Exists(c)) return c;
        }
        return null;
    }

    public void Open(string leftFolder, string rightFolder)
    {
        var exe = FindExecutable()
            ?? throw new InvalidOperationException("WinMerge was selected, but WinMergeU.exe could not be found.");
        Process.Start(CreateStartInfo(exe, leftFolder, rightFolder));
    }

    public static ProcessStartInfo CreateStartInfo(string exe, string leftFolder, string rightFolder)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        psi.ArgumentList.Add("/r");
        psi.ArgumentList.Add("/ul");
        psi.ArgumentList.Add("/ur");
        psi.ArgumentList.Add("/dl");
        psi.ArgumentList.Add("Legacy");
        psi.ArgumentList.Add("/dr");
        psi.ArgumentList.Add(".NET 10");
        psi.ArgumentList.Add(leftFolder);
        psi.ArgumentList.Add(rightFolder);
        return psi;
    }

    internal static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim('"'), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
```

`BeyondCompareDiffTool.cs` — similar discovery for `BCompare.exe` under `Program Files\Beyond Compare *` and PATH; `CreateStartInfo` adds left, right only.

`VsCodeDiffTool.cs` — discover `code.cmd` / `code.exe` on PATH and under `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`; Open uses `-n`, left, right.

`DiffToolDiscovery.cs`:

```csharp
public static class DiffToolDiscovery
{
    public static IReadOnlyList<IDiffTool> GetBuiltIn() =>
    [
        new WinMergeDiffTool(),
        new BeyondCompareDiffTool(),
        new VsCodeDiffTool()
    ];

    public static IReadOnlyList<IDiffTool> GetAvailable() =>
        GetBuiltIn().Where(t => t.IsAvailable()).ToList();
}
```

`EmptyFolder.cs`:

```csharp
public static class EmptyFolder
{
    public static string GetPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "SolutionMapper", "empty");
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 4: Run tests — PASS**

- [ ] **Step 5: Commit** (skip if no git)

---

### Task 7: Spectre UI + Program orchestration

**Files:**
- Create: `SolutionMapper/UI/MappingSummary.cs`
- Create: `SolutionMapper/UI/ProjectPicker.cs`
- Create: `SolutionMapper/UI/DiffToolPicker.cs`
- Modify: `SolutionMapper/Program.cs`

**Interfaces:**
- Consumes: `ProjectMapper`, `MappingExport`, `DiffToolDiscovery`, `EmptyFolder`, `IDiffTool`
- Produces: runnable CLI

CLI parse (no extra package):

```text
args[0] = legacyRoot
args[1] = upgradedRoot
optional: --export <path>
```

Flow:
1. Validate arg count; print usage on failure (exit 1)
2. Resolve full paths; if root missing → print error like design, exit 1
3. `EnsureHasProjects` both sides
4. `var mappings = ProjectMapper.Map(...)`
5. If `--export`, write JSON
6. `MappingSummary.Write(mappings)`
7. `var selected = ProjectPicker.Pick(mappings)` — null/empty → exit 0
8. `var tool = DiffToolPicker.Pick(DiffToolDiscovery.GetAvailable())` — if none available, error
9. For each selected: resolve left/right (use `EmptyFolder.GetPath()` when null); `tool.Open(left, right)` without WaitForExit

`MappingSummary`:

```csharp
public static void Write(IReadOnlyList<ProjectMapping> mappings)
{
    var matched = mappings.Count(m => m.Status == MappingStatus.Matched);
    var ambiguous = mappings.Count(m => m.Status == MappingStatus.Ambiguous || m.IsAmbiguous);
    var legacyOnly = mappings.Count(m => m.Status == MappingStatus.LegacyOnly);
    var upgradedOnly = mappings.Count(m => m.Status == MappingStatus.UpgradedOnly);

    AnsiConsole.MarkupLine("[green]✓[/] {0} matched", matched);
    AnsiConsole.MarkupLine("[yellow]⚠[/] {0} ambiguous", ambiguous);
    AnsiConsole.MarkupLine("[yellow]⚠[/] {0} legacy only", legacyOnly);
    AnsiConsole.MarkupLine("[yellow]⚠[/] {0} upgraded only", upgradedOnly);
}
```

`ProjectPicker`: use Spectre `MultiSelectionPrompt<ProjectMapping>` with filterable titles:

```csharp
public static IReadOnlyList<ProjectMapping>? Pick(IReadOnlyList<ProjectMapping> mappings)
{
    if (mappings.Count == 0) return [];

    var prompt = new MultiSelectionPrompt<ProjectMapping>()
        .Title("Select projects (space to toggle, enter to confirm):")
        .NotRequired()
        .PageSize(15)
        .MoreChoicesText("[grey](Move up/down to reveal more)[/]")
        .InstructionsText("[grey](Filter by typing if supported / space select)[/]")
        .UseConverter(Format);

    prompt.AddChoices(mappings);
    return AnsiConsole.Prompt(prompt);
}

static string Format(ProjectMapping m) => m.Status switch
{
    MappingStatus.Matched => m.Name,
    MappingStatus.Ambiguous => $"{m.Name}  [ambiguous]",
    MappingStatus.LegacyOnly => $"{m.Name}  legacy only",
    MappingStatus.UpgradedOnly => $"{m.Name}  upgraded only",
    _ => m.Name
};
```

Note: Spectre MultiSelectionPrompt search varies by version — if built-in search weak, wrap with a loop: ask `TextPrompt` filter string, then show filtered MultiSelectionPrompt. Prefer filter loop for ~200 projects:

```csharp
while (true)
{
    var filter = AnsiConsole.Prompt(
        new TextPrompt<string>("Search projects (empty = all, Ctrl+C exit):").AllowEmpty());
    var filtered = string.IsNullOrWhiteSpace(filter)
        ? mappings
        : mappings.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    if (filtered.Count == 0) { AnsiConsole.MarkupLine("[yellow]No matches[/]"); continue; }

    var selected = AnsiConsole.Prompt(
        new MultiSelectionPrompt<ProjectMapping>()
            .Title($"Select projects ({filtered.Count} shown):")
            .NotRequired()
            .PageSize(15)
            .UseConverter(Format)
            .AddChoices(filtered));

    if (selected.Count > 0) return selected;
    // empty selection: ask again or return empty
    if (!AnsiConsole.Confirm("Nothing selected. Search again?", true)) return selected;
}
```

`DiffToolPicker`:

```csharp
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
```

`Program.cs` outline:

```csharp
using Spectre.Console;
using SolutionMapper.DiffTools;
using SolutionMapper.Mapping;
using SolutionMapper.UI;

if (args.Length < 2)
{
    AnsiConsole.MarkupLine("Usage: mapper \"<legacy-root>\" \"<upgraded-root>\" [--export mapping.json]");
    return 1;
}

string? exportPath = null;
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--export")
    {
        if (i + 1 >= args.Length) { AnsiConsole.MarkupLine("Error: --export requires a path."); return 1; }
        exportPath = args[++i];
    }
    else positional.Add(args[i]);
}

if (positional.Count != 2)
{
    AnsiConsole.MarkupLine("Usage: mapper \"<legacy-root>\" \"<upgraded-root>\" [--export mapping.json]");
    return 1;
}

var legacyRoot = Path.GetFullPath(positional[0]);
var upgradedRoot = Path.GetFullPath(positional[1]);

try
{
    if (!Directory.Exists(legacyRoot))
    {
        AnsiConsole.MarkupLine($"Error: Legacy solution root does not exist.\n\n  {legacyRoot}");
        return 1;
    }
    if (!Directory.Exists(upgradedRoot))
    {
        AnsiConsole.MarkupLine($"Error: Upgraded solution root does not exist.\n\n  {upgradedRoot}");
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
```

- [ ] **Step 1: Implement UI + Program**

- [ ] **Step 2: Build**

Run: `rtk dotnet build SolutionMapper.sln`  
Expected: succeeded

- [ ] **Step 3: Manual smoke with fixture trees**

```powershell
$legacy = Join-Path $env:TEMP "sm-legacy"; $up = Join-Path $env:TEMP "sm-up"
Remove-Item $legacy,$up -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$legacy\Services\Billing","$up\src\Billing" | Out-Null
Set-Content "$legacy\Services\Billing\Billing.csproj" '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'
Set-Content "$up\src\Billing\Billing.csproj" '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'
rtk dotnet run --project SolutionMapper -- "$legacy" "$up" --export "$env:TEMP\mapping.json"
```

Expected: summary shows 1 matched; picker appears; after select+tool (or Ctrl+C), export JSON exists if reached export before picker — export happens before picker so JSON should exist even if picker cancelled after... actually export is before picker, so after scan JSON exists. Can Ctrl+C at picker.

- [ ] **Step 4: Run full test suite**

Run: `rtk dotnet test SolutionMapper.sln`  
Expected: all PASS

- [ ] **Step 5: Commit** (skip if no git)

```powershell
rtk git commit -am "feat: interactive picker and diff-tool launch"
```

---

### Task 8: Verification gate

- [ ] **Step 1: `rtk dotnet test SolutionMapper.sln`** — all green
- [ ] **Step 2: Confirm exe name** — `rtk dotnet build` then check `SolutionMapper/bin/Debug/net10.0/mapper.exe` (or `mapper.dll`) exists
- [ ] **Step 3: Spec coverage check** — discovery, one-to-one, dup scoring, unmatched, export, WinMerge/BC/VS Code, empty folder, read-only, Spectre picker

---

## Self-review (plan author)

1. **Spec coverage:** Full MVP from design covered across Tasks 1–8. Custom tool intentionally omitted. MAUI omitted.
2. **Placeholders:** None intentional; VS Code folder compare documented as open-both-folders (stock CLI limitation).
3. **Types:** `ProjectMapping` / `MappingStatus` / `IDiffTool` / `ProjectMapper.Map` consistent across tasks.

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-07-30-solution-mapper.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — this session, executing-plans with checkpoints  

Which approach?
