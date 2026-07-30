# Solution Mapper — Design

Date: 2026-07-30  
Source: `solution-mapper-spec.md` + brainstorming decisions

## Goal

Windows developer console tool: given legacy and upgraded .NET solution roots (~200 projects, different folder layouts), map corresponding projects by `.csproj` identity, let the developer search/multi-select, and open each pair of project folders in a diff tool.

Read-only against solution trees. No auto-copy, sync, merge, or semantic analysis.

## Decisions

| Topic | Choice |
|---|---|
| Scope | Full MVP |
| Diff tools | WinMerge, Beyond Compare, VS Code (no Custom) |
| Layout | One console project + one test project |
| Naming | Project `SolutionMapper`, exe `mapper` |
| Runtime | .NET 10, C#, Spectre.Console |
| Persistence | Optional `--export` JSON snapshot only; every run rescans |

## Solution layout

```text
SolutionMapper.sln
├── SolutionMapper/                 # net10.0 console, AssemblyName=mapper
│   ├── Program.cs
│   ├── Mapping/
│   │   ├── ProjectDiscovery.cs
│   │   ├── ProjectMetadataReader.cs
│   │   ├── ProjectMatcher.cs
│   │   ├── ProjectMapper.cs
│   │   └── ProjectMapping.cs
│   ├── UI/
│   │   ├── MappingSummary.cs
│   │   ├── ProjectPicker.cs
│   │   └── DiffToolPicker.cs
│   └── DiffTools/
│       ├── IDiffTool.cs
│       ├── DiffToolDiscovery.cs
│       ├── WinMergeDiffTool.cs
│       ├── BeyondCompareDiffTool.cs
│       └── VsCodeDiffTool.cs
└── SolutionMapper.Tests/           # xUnit; mapping focus
```

Dependencies: Spectre.Console (+ xUnit for tests). No MSBuild evaluation packages.

## Architecture

```text
CLI args → Discovery → Matching → ProjectMapping[]
                ↓
         Summary + Picker (Spectre)
                ↓
         DiffToolPicker → IDiffTool.Open(left, right)
```

Hard rule: mapping layer knows nothing about diff tools. Diff tools know nothing about projects — only folder pairs.

## CLI

```text
mapper "<legacy-root>" "<upgraded-root>" [--export mapping.json]
```

Validate roots exist. Fail clearly if a root is missing or either side has no `.csproj`.

## Mapping model

```csharp
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

public enum MappingStatus
{
    Matched,
    LegacyOnly,
    UpgradedOnly,
    Ambiguous
}
```

Comparison boundary for a matched project: the directory containing the `.csproj`.

## Matching algorithm

1. Recursively discover `*.csproj` under each root (`Directory.EnumerateFiles`).
2. Group by project filename with case-insensitive comparison (Windows).
3. One legacy + one upgraded for a filename → `Status=Matched`, `IsAmbiguous=false`, confidence `1.0`.
4. Only one side → `LegacyOnly` / `UpgradedOnly` (`IsAmbiguous=false`).
5. Duplicates → score candidates and pair strongest unused:
   - Same filename: +100
   - Same AssemblyName: +50
   - Same TargetFramework(s): +10
   - Same RootNamespace: +5
   - Similar ProjectReference names: +10
6. Auto-resolved duplicate pairs → `Status=Ambiguous`, `IsAmbiguous=true`, still pick strongest candidate (summary counts these as ambiguous).
7. Do not attempt sophisticated rename detection in MVP.

Metadata via `XDocument` only when disambiguation needs it. Absent, conditional, or unevaluable properties are ignored. Corrupt/unreadable csproj: warn and continue the scan.

## Export

Optional JSON snapshot after mapping. Not the source of truth. Shape includes roots plus per-project fields from the mapping model (`status`, `matchConfidence`, `isAmbiguous`, folder/file paths).

## UI

1. Print concise summary counts: matched, ambiguous, legacy-only, upgraded-only.
2. Spectre.Console searchable multi-select list (~200 projects). Type to filter, navigate, multi-select, Enter continue, Escape exit/back.
3. Show unmatched projects with a warning label.
4. Diff tool picker listing available discovered tools (WinMerge, Beyond Compare, VS Code).
5. Launch each selected folder pair without waiting for the tool to exit.

Unmatched selection: open existing folder against `%TEMP%\SolutionMapper\empty\` (create if needed) so tools receive two valid paths.

## Diff tools

```csharp
public interface IDiffTool
{
    string Name { get; }
    bool IsAvailable();
    string? FindExecutable();
    void Open(string leftFolder, string rightFolder);
}
```

Discovery: common install paths + PATH (registry optional if cheap). Prefer `ProcessStartInfo.ArgumentList`.

- **WinMerge:** recursive folder compare (`/r`), unread flags / pane labels as appropriate for installed version.
- **Beyond Compare:** two folder paths; keep BC-specific options inside the adapter.
- **VS Code:** include if discovery finds it; folder-compare semantics live only in the adapter.
- **Custom:** out of MVP scope.

## Safety

Allowed: read projects/dirs, launch diff apps, create temp empty dir, write optional export JSON.

Forbidden: copy/delete/modify source, edit csproj/sln, sync folders, rename projects.

## Errors

Clear, actionable messages for missing roots, no projects found, and missing selected diff-tool executable. No broad swallowing `try/catch`.

## Testing

xUnit tests against temp-directory fixtures:

- one-to-one match
- different nesting, same filename
- duplicate filenames → scoring picks strongest
- legacy-only / upgraded-only
- no projects → error
- corrupt csproj → continue
- case-insensitive filename match

No Spectre e2e UI tests. Diff-tool arg/availability stubs optional if cheap.

## Non-goals

Automatic copy/sync/merge/migration; rename AI matching; full MSBuild evaluation; Git integration; solution-wide virtual trees / symlinks / WinMerge workspaces; MAUI UI; Custom diff tool.

## Success criterion

```powershell
mapper "C:\Legacy" "C:\Net10"
```

→ scan/map → search `Billing` → select → choose WinMerge → WinMerge opens the two real project folders. Developer decides what to copy.
