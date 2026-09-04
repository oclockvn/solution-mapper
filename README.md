# Solution Mapper

Windows console tool that maps projects between a **legacy** .NET solution tree and an **upgraded** one when folder layouts differ. You pick a matched pair and open both project folders in a diff tool. The tool is **read-only** — it never copies or syncs files.

Binary: `mapper.exe`.

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build SolutionMapper.sln
```

Debug output:

`SolutionMapper\bin\Debug\net10.0\mapper.exe`

## Run

With roots on the command line:

```powershell
.\SolutionMapper\bin\Debug\net10.0\mapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root"
```

Or interactive (prompts for missing roots; offers last roots if saved; Tab completes paths):

```powershell
.\SolutionMapper\bin\Debug\net10.0\mapper.exe
```

Or from the output folder:

```powershell
cd SolutionMapper\bin\Debug\net10.0
.\mapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root"
```

### Optional JSON export

```powershell
.\mapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root" --export mapping.json
```

## Dependency closure

After picking one or more projects, the tool offers to **include transitive project dependencies**.
It walks `<ProjectReference>` edges in the legacy tree (resolving each reference relative to its
`.csproj`, falling back to a unique same-named project when the path layout changed).

To keep the result manageable:

- **Max depth** — you're asked how deep to follow references (`1` = direct references only,
  blank = the whole closure).
- **Curate** — the closure is shown as a checklist. Root projects and matched/missing nodes are
  pre-checked; ambiguous ones are left unchecked. Untick anything you don't want to diff.

Each node shows its depth (`L1`, `L2`, …) and mapping status (`✓` matched, `?` ambiguous,
`✗` missing on one side), so a dependency that didn't get migrated is obvious. References that
resolve outside the legacy root are listed and skipped.

### One diff window instead of many

When you diff more than one project, **WinMerge** opens them as tabs in a single window
(via a generated `.WinMerge` project file — needs WinMerge 2.16.4+). Beyond Compare and VS Code
have no multi-tab CLI, so they open one window per project after a confirmation prompt.

### Build output is hidden

WinMerge comparisons apply the **Visual C# loose** filter, which hides `bin`, `obj`, `.vs`,
`.git`, compiled binaries and per-user files. If your WinMerge install doesn't ship that filter,
an equivalent one is generated to a temp `.flt` and used instead.

## Diff tools

If installed, these are discovered automatically:

- WinMerge
- Beyond Compare
- VS Code

## Notes

- Roots are scanned for `*.csproj`; matching is by project name / path heuristics.
- You review diffs yourself — no auto-copy, no folder sync.
- Design notes live under `docs\` and `solution-mapper-spec.md` if you need deeper detail.
