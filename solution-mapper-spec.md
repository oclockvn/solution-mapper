# Solution Mapper — Project Folder Comparison Tool

## 1. Purpose

Build a small Windows developer utility that makes it easy to compare a legacy .NET solution against a newly upgraded .NET 10 solution when the project directory structures have changed significantly.

### Problem

We have two .NET solutions:

- A **legacy** solution.
- A **new/upgraded .NET 10** solution.
- Approximately **200 projects**.
- The solution-level folder structures are substantially different.
- Within an individual project, the folder/file structure is expected to remain mostly the same.

Example:

```text
Legacy:
C:\Legacy\OldStructure\Services\Billing\Billing.csproj

.NET 10:
C:\Net10\src\Components\Billing\Billing.csproj
```

The goal is to identify that both `Billing.csproj` files represent the same project, then allow a developer to compare:

```text
C:\Legacy\OldStructure\Services\Billing
        ↕
C:\Net10\src\Components\Billing
```

The developer will decide what differences are meaningful and what files should be copied. **The tool must not automatically synchronize or copy files.**

---

# 2. Core Goal

The tool should answer one question:

> "Given these two solution roots, where is the corresponding project folder in each solution?"

Then let the developer select projects and open the two corresponding folders in a diff tool.

The application should be intentionally simple.

It is **not** intended to:

- Perform an automatic migration.
- Decide which changes should be copied.
- Synchronize folders.
- Modify source files.
- Perform semantic code analysis.
- Detect every possible project rename.
- Determine whether a change is intentional.
- Produce a sophisticated migration report.

The developer remains responsible for reviewing differences and deciding what to copy.

---

# 3. Recommended Architecture

Use a **.NET 10 console application** initially.

Do **not** build a MAUI GUI for the first version.

The architecture should be separated so a MAUI UI can be added later if useful.

```text
                    ┌──────────────────────────┐
                    │ mapper.exe                │
                    │                          │
                    │ legacyRoot               │
                    │ upgradedRoot             │
                    └────────────┬─────────────┘
                                 │
                           Scan *.csproj
                                 │
                    ┌────────────▼─────────────┐
                    │ Project Mapper            │
                    │                           │
                    │ Project matching          │
                    │ Metadata validation       │
                    │ Mapping creation          │
                    └────────────┬─────────────┘
                                 │
                         ProjectMapping[]
                                 │
                    ┌────────────▼─────────────┐
                    │ Interactive Picker        │
                    │                           │
                    │ Search + multi-select     │
                    └────────────┬─────────────┘
                                 │
                         Select diff tool
                                 │
              ┌──────────────────┼──────────────────┐
              ▼                  ▼                  ▼
           WinMerge        Beyond Compare       Future...
```

Important architectural principle:

> **The mapping engine must not know anything about WinMerge, Beyond Compare, or any other diff tool.**

Likewise:

> **Diff tool implementations must not know anything about projects or .NET solutions.**

The mapping layer produces folder pairs. The diff-tool layer consumes folder pairs.

---

# 4. User Experience

The main command should be:

```powershell
mapper "C:\LegacySolution" "C:\Net10Solution"
```

The application scans both roots and displays a summary.

Example:

```text
Solution Mapper
────────────────────────────────────────────

Legacy:
  C:\LegacySolution

Upgraded:
  C:\Net10Solution

Scanning projects...

✓ 193 matched
⚠ 3 ambiguous mappings resolved automatically
⚠ 2 legacy-only projects
⚠ 1 upgraded-only project

Search projects: _
```

The developer gets an interactive searchable list.

Example:

```text
Search projects: billing

  ○ Billing
  ○ Billing.Api
  ○ Billing.Tests
  ⚠ Billing.Legacy        legacy only
  ⚠ Billing.New           upgraded only
```

The developer can:

- Type to search.
- Navigate results.
- Multi-select projects.
- Press Enter to continue.
- Press Escape to go back/exit.

After selection:

```text
Selected projects:

  Billing
  Billing.Api
  Billing.Tests

Select comparison tool:

> WinMerge
  Beyond Compare
  VS Code
  Custom
```

The application then opens the selected folder pairs in the selected tool.

---

# 5. CLI Arguments

The two solution roots are supplied as command-line arguments.

Required:

```powershell
mapper "<legacy-root>" "<upgraded-root>"
```

Example:

```powershell
mapper "D:\src\legacy" "D:\src\net10"
```

Do not require a configuration file for normal usage.

---

# 6. Optional Mapping Export

The tool should optionally export the generated mapping for debugging, inspection, or reproducibility.

Example:

```powershell
mapper "D:\src\legacy" "D:\src\net10" --export mapping.json
```

Important:

> The exported JSON is not the source of truth.

Every normal execution should rescan both solutions and calculate the mapping again.

The export is only an optional snapshot.

Example:

```json
{
  "projects": [
    {
      "name": "Billing",
      "projectFile": "Billing.csproj",
      "legacy": "D:\\src\\legacy\\old\\Billing",
      "upgraded": "D:\\src\\net10\\src\\Billing",
      "matchConfidence": 1.0,
      "status": "Matched"
    }
  ]
}
```

---

# 7. Project Discovery

Recursively discover `.csproj` files beneath each root.

Conceptually:

```csharp
Directory.EnumerateFiles(
    root,
    "*.csproj",
    SearchOption.AllDirectories)
```

Do not load/evaluate the projects through MSBuild for the MVP.

The tool only needs lightweight metadata.

This means the application should not require a complete Visual Studio/MSBuild project environment merely to perform mapping.

---

# 8. Project Identity / Matching

The primary identity is the `.csproj` filename.

Example:

```text
Legacy:
A/Foo/Foo.csproj

New:
src/SomeOtherStructure/Foo/Foo.csproj
```

These are candidate matches because both are:

```text
Foo.csproj
```

The tool should use lightweight metadata to improve matching when duplicate project filenames exist.

## Primary matching

Match by:

```text
Project filename
```

Example:

```text
Foo.csproj ↔ Foo.csproj
```

## Secondary validation

When needed, inspect lightweight XML properties such as:

```xml
<AssemblyName>Foo</AssemblyName>
<TargetFramework>net10.0</TargetFramework>
<RootNamespace>Foo</RootNamespace>
```

Potentially inspect:

```xml
<ProjectReference Include="..." />
```

to improve confidence.

Do not over-engineer this.

---

# 9. Duplicate Project Filenames

Example:

```text
Legacy:
  Services/Foo/Foo.csproj
  Tests/Foo/Foo.csproj

New:
  src/Foo/Foo.csproj
```

There are two legacy candidates.

The mapper should attempt best-effort automatic selection.

A simple scoring model is sufficient.

Example:

```text
Same .csproj filename              +100
Same AssemblyName                    +50
Same TargetFramework                 +10
Same RootNamespace                    +5
Similar ProjectReference names      +10
```

Example:

```text
Foo.csproj

Legacy candidates:

  Services/Foo/Foo.csproj       score 165
  Tests/Foo/Foo.csproj          score 105

New:
  src/Foo/Foo.csproj

→ Select Services/Foo/Foo.csproj
```

The exact scoring values are implementation details and can be adjusted.

The important behavior is:

1. Prefer the strongest candidate.
2. Do not require perfect certainty.
3. Mark weak/ambiguous mappings.
4. Show the developer a warning in the summary.

Example:

```text
⚠ 3 ambiguous mappings resolved automatically
```

Do not build an advanced project similarity/AI matching system.

---

# 10. Renamed Projects

Do **not** attempt sophisticated renamed-project discovery in the MVP.

For example:

```text
Legacy:
OrderService.csproj

New:
OrderProcessing.csproj
```

If neither filename nor lightweight metadata provides a reliable match, it is acceptable for the project to remain unmatched.

The tool's purpose is to quickly map the normal ~200-project migration, not to solve every possible project identity transformation.

---

# 11. Project Mapping Model

Use a simple model similar to:

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
```

Possible status values:

```csharp
public enum MappingStatus
{
    Matched,
    LegacyOnly,
    UpgradedOnly,
    Ambiguous
}
```

The exact model can be simplified if appropriate.

---

# 12. Comparison Boundary

For a matched project:

> **The folder containing the `.csproj` is the comparison boundary.**

Example:

```text
Legacy:
C:\Legacy\Services\Foo\Foo.csproj

Upgraded:
C:\Net10\src\Foo\Foo.csproj
```

Compare:

```text
C:\Legacy\Services\Foo
        ↕
C:\Net10\src\Foo
```

Do not automatically follow files outside the project folder.

Do not resolve shared files into the comparison.

Do not include arbitrary solution-level files in a project comparison.

This keeps the behavior predictable.

---

# 13. Build Artifacts / Generated Files

The intended comparison is source/project content.

Do not treat build artifacts as meaningful migration differences.

Typical directories to exclude include:

```text
bin/
obj/
.vs/
TestResults/
```

Potentially other generated/user-specific files:

```text
*.user
*.suo
```

However, the mapper itself should not physically filter/copy files.

The preferred design is:

```text
Mapper
  ↓
real project folder paths
  ↓
Diff tool
  ↓
diff-tool-specific filtering
```

For WinMerge, configure the appropriate folder/file filter.

Keep the initial exclusion list small and configurable later.

---

# 14. Unmatched Projects

A project can exist only on one side.

Example:

```text
Legacy:
  Foo.csproj
  Bar.csproj

Upgraded:
  Foo.csproj
  Baz.csproj
```

Result:

```text
Foo   → matched
Bar   → legacy only
Baz   → upgraded only
```

The interactive picker should show unmatched projects.

Example:

```text
  ✓ Foo
  ⚠ Bar                 legacy only
  ⚠ Baz                 upgraded only
```

Selecting an unmatched project should ideally open the existing folder against an empty folder.

Example:

```text
Legacy only:

C:\Legacy\Bar
<empty>
```

or:

```text
Upgraded only:

<empty>
C:\Net10\Baz
```

For WinMerge, an actual empty temporary directory may be required because both folder paths need to be valid.

Suggested temporary directory:

```text
%TEMP%\SolutionMapper\empty\
```

Create it if necessary.

This can be implemented in the MVP if easy, otherwise it can be deferred until after matched-project comparisons work.

---

# 15. Diff Tool Abstraction

Use an abstraction similar to:

```csharp
public interface IDiffTool
{
    string Name { get; }

    bool IsAvailable();

    void Open(string leftFolder, string rightFolder);
}
```

Potential implementations:

```text
WinMergeDiffTool
BeyondCompareDiffTool
VsCodeDiffTool
CustomDiffTool
```

The mapping engine should only produce:

```text
leftFolder
rightFolder
```

The diff tool handles the command-line invocation.

---

# 16. WinMerge Integration

WinMerge is the first recommended implementation.

The basic operation is equivalent to:

```text
WinMergeU.exe "C:\Legacy\Foo" "C:\Net10\Foo"
```

Use recursive folder comparison.

Conceptually, the command can include:

```text
/r
```

Potential useful options:

```text
/ul
/ur
/dl "Legacy"
/dr ".NET 10"
```

The exact command-line options should be implemented based on the installed/current WinMerge version.

Prefer `ProcessStartInfo.ArgumentList` rather than manually constructing a single argument string.

Example:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = executablePath,
    UseShellExecute = true
};

startInfo.ArgumentList.Add("/r");
startInfo.ArgumentList.Add("/ul");
startInfo.ArgumentList.Add("/ur");
startInfo.ArgumentList.Add("/dl");
startInfo.ArgumentList.Add("Legacy");
startInfo.ArgumentList.Add("/dr");
startInfo.ArgumentList.Add(".NET 10");
startInfo.ArgumentList.Add(leftFolder);
startInfo.ArgumentList.Add(rightFolder);

Process.Start(startInfo);
```

Validate the exact WinMerge arguments during implementation.

---

# 17. Beyond Compare Integration

Support Beyond Compare as a second adapter.

The basic operation is equivalent to:

```text
BCompare.exe "C:\Legacy\Foo" "C:\Net10\Foo"
```

Beyond Compare can open a folder comparison using the two paths.

Potential options can be added later, such as:

```text
/ro
/expandall
```

Do not make Beyond Compare-specific behavior leak into the core mapping code.

---

# 18. VS Code Integration

VS Code can be considered a third optional adapter.

However, folder comparison semantics may differ from WinMerge/Beyond Compare.

Do not make VS Code a requirement for MVP.

It can be included in the tool list if its behavior is acceptable:

```text
Select comparison tool:

> WinMerge
  Beyond Compare
  VS Code
  Custom
```

---

# 19. Custom Diff Tool

Support a custom executable option eventually.

Example:

```text
Select comparison tool:

> WinMerge
  Beyond Compare
  VS Code
  Custom
```

For `Custom`, prompt for:

```text
Executable path:
Arguments:
```

Alternatively, defer this feature until the built-in tools work.

Do not let custom command-line parsing complicate the initial architecture.

---

# 20. Diff Tool Discovery

The application should have a built-in list of supported tools.

Example:

```text
WinMerge
Beyond Compare
VS Code
Custom
```

For built-in tools, attempt to discover the executable.

Possible discovery mechanisms:

1. Common installation locations.
2. PATH lookup.
3. Registry, if useful.
4. User-selected executable path as fallback.

Do not hard-code only one installation directory.

The user may install WinMerge in a custom location.

Example abstraction:

```csharp
public interface IDiffTool
{
    string Name { get; }

    bool IsAvailable();

    string? FindExecutable();

    void Open(string leftFolder, string rightFolder);
}
```

The exact interface may be refined during implementation.

---

# 21. Interactive Search

Use **Spectre.Console** for the interactive terminal UI.

The project list should support:

- Text search.
- Keyboard navigation.
- Multi-selection.
- Enter to continue.
- Escape to exit/back.

For ~200 projects, a searchable list is preferred over a huge numbered list.

Example:

```text
Search: bill

[x] Billing
[ ] Billing.Api
[ ] Billing.Tests
[ ] Billing.Worker
```

The developer should not have to scroll through all 200 projects.

---

# 22. Multi-select

Allow multiple projects to be selected.

Example:

```text
[x] Billing
[x] Billing.Api
[x] Billing.Tests
[ ] Customer
[ ] Customer.Api
```

After pressing Enter:

```text
Selected: 3 projects
```

Then choose the diff tool once.

The selected comparisons can then be launched.

---

# 23. Launch Behavior

For each selected project:

```text
ProjectMapping
    ↓
LegacyFolder
    ↓
UpgradedFolder
    ↓
IDiffTool.Open(...)
```

Do not wait for the diff tool to finish.

The mapper should launch the requested comparisons and return control to the developer.

Potentially launch each comparison as a separate process.

Be aware that opening 50–200 comparisons simultaneously may be undesirable.

For the MVP, launching the selected projects directly is acceptable.

A future option could limit or queue launches.

---

# 24. Whole-Solution Comparison

Do not implement a virtual normalized directory tree.

Do not copy files into a temporary comparison directory.

Do not create symlinks or junctions merely to make WinMerge see a normalized solution.

The actual model is:

```text
Solution mapping
    ↓
Many independent folder pairs
```

For example:

```text
Billing
    Legacy/Billing
        ↕
    Net10/Billing

Customer
    Legacy/Customer
        ↕
    Net10/Customer

Orders
    Legacy/Orders
        ↕
    Net10/Orders
```

This is simpler, safer, and more faithful to the actual problem.

The tool itself provides the "whole solution" navigation by allowing the developer to search/select multiple projects.

---

# 25. Why Not Generate a WinMerge Workspace?

Do not make WinMerge project/workspace generation the primary architecture.

The mapper already knows the exact two folders:

```text
Legacy project folder
        ↕
Upgraded project folder
```

WinMerge already knows how to compare two folders.

Therefore:

```text
Mapper
   ↓
two real folders
   ↓
WinMerge
```

is enough.

Avoid:

- Artificial directory trees.
- Temporary normalized solution structures.
- Symlinks.
- Junctions.
- File copies.
- WinMerge-specific project files.

This keeps the application independent of the comparison tool.

---

# 26. Mapping vs Diff Tool Separation

This is a core design requirement.

```text
┌──────────────────────────┐
│ Project Discovery        │
│                          │
│ Find *.csproj            │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Project Matching         │
│                          │
│ filename + metadata      │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ ProjectMapping[]         │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Interactive Selection    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ IDiffTool                │
├──────────────────────────┤
│ WinMerge                 │
│ Beyond Compare           │
│ VS Code                  │
└──────────────────────────┘
```

This separation is important because a future GUI can reuse everything except the CLI UI.

---

# 27. Suggested Solution Structure

Initially, keep the codebase small.

Option 1 — one project:

```text
SolutionMapper/
├── Program.cs
├── Mapping/
│   ├── ProjectMapper.cs
│   ├── ProjectMatcher.cs
│   ├── ProjectMetadataReader.cs
│   └── ProjectMapping.cs
├── UI/
│   └── ProjectPicker.cs
└── DiffTools/
    ├── IDiffTool.cs
    ├── WinMergeDiffTool.cs
    ├── BeyondCompareDiffTool.cs
    ├── VsCodeDiffTool.cs
    └── DiffToolDiscovery.cs
```

This is recommended for the MVP.

Do not create many projects just for architectural purity.

If the application grows, split into:

```text
SolutionMapper.sln

SolutionMapper.Cli
SolutionMapper.Core
SolutionMapper.DiffTools
```

---

# 28. Suggested Dependencies

Keep dependencies minimal.

Recommended:

```text
.NET 10
Spectre.Console
```

Potentially a command-line parsing package if useful, but the initial two positional arguments are simple enough that custom parsing may be sufficient.

Do not introduce MSBuild project evaluation packages unless a concrete requirement appears.

---

# 29. Project Metadata Parsing

Use standard XML parsing.

For example:

```csharp
XDocument.Load(projectFile)
```

Extract only properties needed for matching.

Potential metadata:

```text
AssemblyName
TargetFramework
TargetFrameworks
RootNamespace
ProjectReference
```

Be defensive:

- Property may be absent.
- Property may be conditional.
- `AssemblyName` may use MSBuild variables.
- ProjectReference paths may be relative.
- XML may contain namespaces.

For the MVP, if a property cannot be statically resolved, simply don't use it for scoring.

Do not implement full MSBuild evaluation.

---

# 30. Matching Algorithm — Suggested MVP

Pseudo-code:

```text
discover legacy projects
discover upgraded projects

group projects by filename

for each filename:
    legacyCandidates = legacy[filename]
    upgradedCandidates = upgraded[filename]

    if one-to-one:
        create direct mapping

    if one side has no candidates:
        create legacy-only / upgraded-only mappings

    if duplicates:
        score candidates
        choose strongest unused pair
        mark mapping ambiguous when confidence is not strong
```

Avoid overcomplicating this.

The expected common case is:

```text
one legacy Foo.csproj
one upgraded Foo.csproj
```

which should be a direct mapping.

---

# 31. Confidence

Confidence is useful primarily for diagnostics.

Example:

```text
1.00
```

for direct one-to-one filename matches.

Lower values for heuristic matches.

The exact mathematical meaning is not important.

The UI should mainly expose:

```text
✓ matched
⚠ ambiguous
⚠ legacy only
⚠ upgraded only
```

Do not build a sophisticated confidence-scoring UI.

---

# 32. Summary Before Selection

Before displaying the project picker, show a concise summary.

Example:

```text
Project mapping complete

✓ 193 matched
⚠ 3 ambiguous
⚠ 2 legacy only
⚠ 1 upgraded only

Press Enter to select projects.
```

This gives developers confidence that the scan worked.

---

# 33. Error Handling

Important errors should be clear and actionable.

Examples:

```text
Error: Legacy solution root does not exist.

  C:\LegacySolution
```

```text
Error: Upgraded solution root does not exist.

  C:\Net10Solution
```

```text
Error: No .csproj files found under:

  C:\LegacySolution
```

For a missing diff tool:

```text
WinMerge was selected, but WinMergeU.exe could not be found.

Choose another tool or configure the executable path.
```

Do not swallow errors.

Do not use broad unnecessary `try/catch` blocks.

---

# 34. Safety

The application should be **read-only with respect to the source solutions**.

It should:

- Read project files.
- Read directory structures.
- Launch diff applications.
- Optionally create temporary empty directories.
- Optionally export a JSON mapping.

It should NOT:

- Copy source files automatically.
- Delete files.
- Modify `.csproj` files.
- Modify solution files.
- Synchronize folders.
- Rename projects.
- Modify source code.

This is a core requirement.

---

# 35. Testing Strategy

Focus tests on mapping rather than the diff applications.

Test cases:

## Basic one-to-one

```text
Legacy/Foo/Foo.csproj
New/src/Foo/Foo.csproj
```

Expected:

```text
Foo → correct two folders
```

## Different directory structures

Ensure arbitrary nesting doesn't matter.

## Duplicate project names

```text
Legacy/A/Foo.csproj
Legacy/B/Foo.csproj
New/C/Foo.csproj
```

Ensure metadata scoring selects the strongest candidate.

## Legacy-only

```text
Legacy/Foo.csproj
New/none
```

Expected:

```text
Foo → LegacyOnly
```

## Upgraded-only

Expected:

```text
Foo → UpgradedOnly
```

## No projects

Return a clear error.

## Invalid/corrupt csproj

Do not crash the entire scan.

Report the problematic project and continue where reasonable.

## Case sensitivity

Windows filesystems are generally case-insensitive.

Project matching should therefore not rely on case-sensitive filename comparison.

## Nested projects

Ensure recursive discovery works correctly.

---

# 36. Export Format

The exported mapping should be human-readable JSON.

Example:

```json
{
  "legacyRoot": "C:\\Legacy",
  "upgradedRoot": "C:\\Net10",
  "projects": [
    {
      "name": "Billing",
      "projectFileName": "Billing.csproj",
      "legacyProjectFile": "C:\\Legacy\\old\\Billing\\Billing.csproj",
      "upgradedProjectFile": "C:\\Net10\\src\\Billing\\Billing.csproj",
      "legacyFolder": "C:\\Legacy\\old\\Billing",
      "upgradedFolder": "C:\\Net10\\src\\Billing",
      "status": "Matched",
      "matchConfidence": 1.0,
      "isAmbiguous": false
    }
  ]
}
```

This is primarily for troubleshooting and manual inspection.

---

# 37. Non-Goals

Do not implement these unless requirements change:

- Automatic file copying.
- Automatic migration.
- Automatic synchronization.
- Automatic merge.
- Automatic project rename detection.
- Semantic C# comparison.
- Git integration.
- Git diff.
- Full MSBuild evaluation.
- Dependency graph visualization.
- Automatic conflict resolution.
- Automatic decision of which source is authoritative.
- Solution-level artificial filesystem generation.
- WinMerge workspace generation as the core mechanism.
- MAUI UI in MVP.

---

# 38. Future Extensions

Possible future features:

```text
--tool winmerge
--tool beyond-compare
--export mapping.json
--include-unmatched
--exclude-projects ...
```

Potential future GUI:

```text
MAUI GUI
│
├── Legacy root picker
├── Upgraded root picker
├── Project search
├── Project multi-selection
├── Mapping warnings
└── Diff tool selection
```

The GUI should reuse the same:

```text
ProjectMapper
ProjectMatcher
ProjectMapping
IDiffTool
```

Only the presentation layer changes.

---

# 39. MVP Implementation Order

Implement in this order.

## Step 1 — Project discovery

Given:

```text
legacyRoot
upgradedRoot
```

find all `.csproj` files.

## Step 2 — Basic mapping

Match by `.csproj` filename.

## Step 3 — Mapping model

Represent:

```text
LegacyFolder
UpgradedFolder
Status
```

## Step 4 — Lightweight metadata

Read:

```text
AssemblyName
TargetFramework
RootNamespace
ProjectReference
```

only when required for disambiguation.

## Step 5 — Duplicate handling

Implement simple scoring.

## Step 6 — Summary

Display:

```text
matched
ambiguous
legacy-only
upgraded-only
```

## Step 7 — Interactive picker

Use Spectre.Console.

Requirements:

- Search.
- Multi-select.
- Keyboard navigation.

## Step 8 — Diff abstraction

Implement:

```csharp
IDiffTool
```

## Step 9 — WinMerge

Implement and test actual executable discovery and launch.

## Step 10 — Beyond Compare

Add the second adapter.

## Step 11 — Optional export

Implement:

```text
--export mapping.json
```

## Step 12 — Unmatched project comparison

Add temporary empty-folder support.

## Step 13 — Tests

Add mapping tests and edge cases.

---

# 40. Example End-to-End Scenario

Given:

```text
Legacy:
C:\Legacy
├── Services
│   ├── Billing
│   │   └── Billing.csproj
│   └── Customer
│       └── Customer.csproj
└── Tests
    └── Billing.Tests
        └── Billing.Tests.csproj
```

and:

```text
.NET 10:
C:\Net10
├── src
│   ├── Billing
│   │   └── Billing.csproj
│   └── Customer
│       └── Customer.csproj
└── test
    └── Billing.Tests
        └── Billing.Tests.csproj
```

Run:

```powershell
mapper "C:\Legacy" "C:\Net10"
```

Mapper produces:

```text
Billing
    Legacy:
        C:\Legacy\Services\Billing
    Upgraded:
        C:\Net10\src\Billing

Customer
    Legacy:
        C:\Legacy\Services\Customer
    Upgraded:
        C:\Net10\src\Customer

Billing.Tests
    Legacy:
        C:\Legacy\Tests\Billing.Tests
    Upgraded:
        C:\Net10\test\Billing.Tests
```

Developer searches:

```text
Search: billing
```

Selects:

```text
[x] Billing
[x] Billing.Tests
```

Selects:

```text
> WinMerge
```

The application launches:

```text
WinMergeU.exe "C:\Legacy\Services\Billing" "C:\Net10\src\Billing"

WinMergeU.exe "C:\Legacy\Tests\Billing.Tests" "C:\Net10\test\Billing.Tests"
```

The developer reviews the differences and decides what to copy.

---

# 41. Important Design Principle

The project should stay focused on this workflow:

```text
Find corresponding projects
        ↓
Find their containing folders
        ↓
Let developer select projects
        ↓
Open two folders in a diff tool
```

Do not turn this into a migration framework.

The application is essentially a **smart folder-pair launcher** for large .NET solution migrations.

That simplicity is a feature.

---

# 42. Recommended Technology

| Concern | Recommendation |
|---|---|
| Runtime | .NET 10 |
| Language | C# |
| UI | Console initially |
| Interactive UI | Spectre.Console |
| Project discovery | `Directory.EnumerateFiles` |
| Project metadata | `XDocument` / XML |
| Project evaluation | Not required |
| Mapping | Filename + lightweight metadata |
| Diff abstraction | `IDiffTool` |
| First diff tool | WinMerge |
| Second diff tool | Beyond Compare |
| Optional third | VS Code |
| Configuration | CLI arguments |
| Mapping persistence | Optional JSON export |
| GUI | Future MAUI option |
| Source modification | Never |

---

# 43. Final Target

The final MVP should allow a developer to run:

```powershell
mapper "C:\LegacySolution" "C:\Net10Solution"
```

and within seconds:

1. Scan both solutions.
2. Find approximately 200 projects.
3. Automatically map corresponding projects.
4. Report ambiguous/unmatched projects.
5. Search/filter projects interactively.
6. Select one or multiple projects.
7. Choose WinMerge/Beyond Compare.
8. Open the correct folder pairs.
9. Leave all migration decisions to the developer.

No manual path hunting.

No copying.

No synchronization.

No generated solution tree.

No complicated migration engine.

Just:

```text
Legacy solution
      +
.NET 10 solution
      ↓
Project mapping
      ↓
Search + select
      ↓
Open folder comparison
```

---

# 44. Implementation Guidance for Cursor

When implementing this project, prioritize:

1. **Correctness of folder mapping.**
2. **Simple code.**
3. **Small number of dependencies.**
4. **Clear separation between mapping and diff-tool launching.**
5. **Good error messages.**
6. **Fast scanning for ~200 projects.**
7. **Read-only behavior against source solutions.**

Avoid premature abstractions.

Avoid implementing features not described in this specification.

The tool should be easy for another developer to understand and modify.

The first successful milestone is:

```powershell
mapper "C:\Legacy" "C:\Net10"
```

→ select `Billing`

→ choose `WinMerge`

→ WinMerge opens:

```text
C:\Legacy\some\old\Billing
        ↕
C:\Net10\some\new\Billing
```

Once that works reliably, add the remaining functionality incrementally.
