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

## After the summary

Before the project search, the tool asks **What now?**:

- **Search & diff projects** (default) — the normal flow below.
- **List unmatched projects** — prints every legacy-only and upgraded-only project as its
  `.csproj` path (relative to that side's root), grouped by side, then returns to the menu.

The menu is skipped when nothing is unmatched.

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

## Releasing a new NuGet version

The tool is published to [nuget.org](https://www.nuget.org/packages/SolutionMapper) as a
.NET global tool (`PackAsTool`, command `SolutionMapper`). Two GitHub Actions workflows drive it:

- **CI** (`.github/workflows/ci.yml`) — runs on every push to `master` and every PR: restore,
  build (Release), test. It publishes nothing; it's just the gate.
- **Publish** (`.github/workflows/publish.yml`) — runs only when a tag matching `v*` is pushed:
  determine version → test → `dotnet pack` → `dotnet nuget push` to nuget.org.

The package version is **not** stored in the `.csproj`. It's computed by
[GitVersion](https://gitversion.net/) from git history and the tag you push (`GitVersion.yml`,
`mode: ContinuousDelivery`, tag prefix `v` or `V`). Tag `v1.4.0` produces package version `1.4.0`.

### Steps

1. Merge your changes to `master` via PR and let CI pass.
2. Pull the latest `master` locally:

   ```powershell
   git checkout master
   git pull --ff-only
   ```

3. Pick the next [SemVer](https://semver.org/) (e.g. `v1.3.1` for a fix, `v1.4.0` for a feature),
   tag the merge commit, and push the tag:

   ```powershell
   git tag v1.4.0
   git push origin v1.4.0
   ```

   The tag **must** start with `v` (or `V`) — a bare `1.4.0` tag won't trigger the workflow.

4. Watch the **Publish** run in the Actions tab. It runs in the `prod` environment; if that
   environment requires a reviewer, approve it. NuGet auth uses OIDC trusted publishing
   (`NuGet/login`), so no API key is stored in the repo.
5. Once the run is green, verify at `https://www.nuget.org/packages/SolutionMapper` (indexing
   takes a few minutes), then:

   ```powershell
   dotnet tool install --global SolutionMapper --version 1.4.0
   ```

### Notes

- **nuget.org versions are immutable.** `dotnet nuget push` uses `--skip-duplicate`, so
  re-pushing an existing version is a no-op. To ship a fix, cut a new tag/version.
- If you tag the wrong commit, delete the tag locally and remotely
  (`git push origin :refs/tags/v1.4.0`) and re-tag **before** the Publish run finishes its push.
  After it reaches nuget.org, that version number is spent.
- Prerequisites (already set up): repo variable `NUGET_USERNAME`, a nuget.org trusted-publishing
  policy for this repo/workflow, and the `prod` GitHub environment.

## Notes

- Roots are scanned for `*.csproj`; matching is by project name / path heuristics.
- You review diffs yourself — no auto-copy, no folder sync.
- Design notes live under `docs\` and `solution-mapper-spec.md` if you need deeper detail.
