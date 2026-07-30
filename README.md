# Solution Mapper

Windows console tool that maps projects between a **legacy** .NET solution tree and an **upgraded** one when folder layouts differ. You pick a matched pair and open both project folders in a diff tool. The tool is **read-only** — it never copies or syncs files.

Binary name: `mapper.exe` (project: `SolutionMapper`).

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

Or interactive (prompts for missing roots):

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

## Diff tools

If installed, these are discovered automatically:

- WinMerge
- Beyond Compare
- VS Code

## Notes

- Roots are scanned for `*.csproj`; matching is by project name / path heuristics.
- You review diffs yourself — no auto-copy, no folder sync.
- Design notes live under `docs\` and `solution-mapper-spec.md` if you need deeper detail.
