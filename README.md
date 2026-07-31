# Solution Mapper

Windows console tool that maps projects between a **legacy** .NET solution tree and an **upgraded** one when folder layouts differ. You pick a matched pair and open both project folders in a diff tool. The tool is **read-only** — it never copies or syncs files.

Binary / tool command: `SolutionMapper`.

## Install

```powershell
dotnet tool install -g SolutionMapper
SolutionMapper "C:\path\to\legacy-root" "C:\path\to\upgraded-root"
# update later:
dotnet tool update -g SolutionMapper
```

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build SolutionMapper.sln
```

Debug output:

`SolutionMapper\bin\Debug\net10.0\SolutionMapper.exe`

## Run

With roots on the command line:

```powershell
.\SolutionMapper\bin\Debug\net10.0\SolutionMapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root"
```

Or interactive (prompts for missing roots; offers last roots if saved; Tab completes paths):

```powershell
.\SolutionMapper\bin\Debug\net10.0\SolutionMapper.exe
```

Or from the output folder:

```powershell
cd SolutionMapper\bin\Debug\net10.0
.\SolutionMapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root"
```

### Optional JSON export

```powershell
.\SolutionMapper.exe "C:\path\to\legacy-root" "C:\path\to\upgraded-root" --export mapping.json
```

## Diff tools

If installed, these are discovered automatically:

- WinMerge
- Beyond Compare
- VS Code

## Notes

- Roots are scanned for `*.csproj`; matching is by project name / path heuristics.
- You review diffs yourself — no auto-copy, no folder sync.
- Design notes live under `docs\` and `solution-mapper-spec.md` if you need deeper detail.
- Releases: tag `v1.2.3` and push — GitHub Actions packs with GitVersion and publishes to NuGet.org via [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC; no long-lived API key). Set repo variable `NUGET_USERNAME` and a nuget.org Trusted Publishing policy for workflow `publish.yml`.
