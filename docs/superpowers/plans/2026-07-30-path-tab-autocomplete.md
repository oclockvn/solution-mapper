# Path Tab Autocomplete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Interactive path entry with Tab suggesting child folders/files via a selection list, shared by all path prompts.

**Architecture:** Pure `PathCompletion` helpers (parse + list matches) unit-tested; `PathAutocompletePrompt` custom key loop + Spectre `SelectionPrompt` on Tab; `Program.cs` wires roots (`Directory`) and export (`FileOrDirectory`).

**Tech Stack:** .NET 10, C#, Spectre.Console, xUnit

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-30-path-tab-autocomplete-design.md`
- ASCII-only UI labels (no Unicode arrows)
- Match: case-insensitive prefix on last path segment
- Cap suggestions at 50; note truncation in SelectionPrompt title
- Inaccessible dirs: skip, do not throw
- Shell: prefix with `rtk`; on this Windows host use `required_permissions: ["all"]` for shell
- Prefer `rtk dotnet test ... -p:UseAppHost=false` if `mapper.exe`/`mapper.dll` is locked
- Commit after each task; work on feature branch `feature/path-tab-autocomplete`
- Do not commit `mapping.json` or secrets

## File structure

| Path | Responsibility |
|---|---|
| `SolutionMapper/UI/PathKind.cs` | `Directory` / `FileOrDirectory` enum |
| `SolutionMapper/UI/PathCompletion.cs` | Parse buffer → parent+prefix; list matches (testable) |
| `SolutionMapper/UI/PathAutocompletePrompt.cs` | Key loop + Tab SelectionPrompt + validate |
| `SolutionMapper/Program.cs` | Use helper for roots + export |
| `SolutionMapper.Tests/UI/PathCompletionTests.cs` | Pure parse/filter tests |

---

### Task 1: PathCompletion helpers + tests

**Files:**
- Create: `SolutionMapper/UI/PathKind.cs`
- Create: `SolutionMapper/UI/PathCompletion.cs`
- Create: `SolutionMapper.Tests/UI/PathCompletionTests.cs`

**Interfaces:**
- Produces:
  - `enum PathKind { Directory, FileOrDirectory }`
  - `readonly record struct PathParseResult(string? ParentDirectory, string Prefix, bool SuggestDrives)`
  - `static PathParseResult PathCompletion.Parse(string buffer)`
  - `static IReadOnlyList<string> PathCompletion.GetSuggestions(string buffer, PathKind kind, int max = 50)` — returns full paths to append candidates (or just names?); design: return **entry names** relative to parent (folder names / file names / drive strings like `D:\`), caller appends
  - Prefer returning list of suggestion strings that replace/extend the last segment: e.g. for buffer `D:\p` return `["projects", "public"]` or full `D:\projects` — **use full completed path strings without trailing slash for files; folders with trailing `\`** so append is replace-buffer-with-suggestion

Define clearly:

```csharp
public static class PathCompletion
{
    public const int DefaultMax = 50;

    public static PathParseResult Parse(string buffer);

    /// <summary>Suggestions are complete path prefixes to replace the buffer (dirs end with \).</summary>
    public static IReadOnlyList<string> GetSuggestions(string buffer, PathKind kind, int max = DefaultMax);
}
```

Parse rules (Windows-oriented):
- Empty / whitespace → `SuggestDrives=true`, Parent=null, Prefix=""
- Ends with `\` or `/` → Parent=that path, Prefix=""
- Else Parent=`Path.GetDirectoryName` of buffer (handle `D:\p` → `D:\`), Prefix=`Path.GetFileName(buffer)`
- `GetSuggestions`: if SuggestDrives → `Environment.GetLogicalDrives()` capped; else enumerate parent (dirs only or dirs+files), filter `StartsWith(prefix, OrdinalIgnoreCase)`, sort, take max; dirs formatted with trailing `Path.DirectorySeparatorChar`

- [ ] **Step 1: Write failing tests** in `PathCompletionTests.cs` covering Parse for `D:\`, `D:\p`, `D:\proj\`, empty; GetSuggestions with a real temp dir fixture (create parent with `alpha`, `beta`, `projects` folders; buffer parent+`p` → only `projects`)

- [ ] **Step 2: Run** `rtk dotnet test SolutionMapper.sln -p:UseAppHost=false --filter PathCompletionTests` — expect FAIL

- [ ] **Step 3: Implement** PathKind + PathCompletion minimal

- [ ] **Step 4: Run tests** — expect PASS

- [ ] **Step 5: Commit** `feat: add path completion parse and suggestions helpers`

---

### Task 2: PathAutocompletePrompt key loop

**Files:**
- Create: `SolutionMapper/UI/PathAutocompletePrompt.cs`
- Consumes: PathCompletion, PathKind, Spectre.Console

**Interfaces:**

```csharp
public static class PathAutocompletePrompt
{
    public static string Prompt(
        string title,
        PathKind kind,
        Func<string, ValidationResult>? validate = null);
}
```

Behavior per design § Key / UI flow:
- Show title; maintain buffer; redraw line (simple: WriteLine title once, then Write `\r` or clear line for buffer — keep simple: print `> {buffer}` and on each change rewrite with AnsiConsole)
- Tab → GetSuggestions; 0 → yellow "no matches"; 1 → set buffer to that suggestion; N → SelectionPrompt of suggestions → set buffer
- Enter → if validate null, default: Directory must Exist as dir; FileOrDirectory: non-empty path (file may not exist yet for new export — **validate in Program**). Prompt method: if validate provided use it; else Directory → must exist directory; FileOrDirectory → non-whitespace only
- Esc → throw `OperationCanceledException` or return and let Program catch — use `OperationCanceledException`
- Backspace / printable chars
- No Unicode in messages

- [ ] **Step 1: Implement PathAutocompletePrompt** (manual key console; unit tests optional for this task — pure logic already in Task 1)

- [ ] **Step 2: `rtk dotnet build SolutionMapper.sln -p:UseAppHost=false`** — succeed

- [ ] **Step 3: Commit** `feat: add Tab path autocomplete prompt`

---

### Task 3: Wire Program.cs

**Files:**
- Modify: `SolutionMapper/Program.cs` — replace `PromptExistingDirectory` with PathAutocompletePrompt; export path too

```csharp
legacyRoot = PathAutocompletePrompt.Prompt(
    "Legacy solution root:",
    PathKind.Directory,
    p => Directory.Exists(p)
        ? ValidationResult.Success()
        : ValidationResult.Error("Directory does not exist."));

// same for upgraded

exportPath = PathAutocompletePrompt.Prompt(
    "Export path:",
    PathKind.FileOrDirectory,
    p => string.IsNullOrWhiteSpace(p)
        ? ValidationResult.Error("Path is required.")
        : ValidationResult.Success());
```

Catch `OperationCanceledException` → print canceled, return 1.

- [ ] **Step 1: Wire Program.cs**, remove old PromptExistingDirectory

- [ ] **Step 2: Full test suite** `rtk dotnet test SolutionMapper.sln -p:UseAppHost=false` — all pass

- [ ] **Step 3: Commit** `feat: use path Tab autocomplete for interactive roots and export`

- [ ] **Step 4: Update README** one line noting Tab completes paths — commit `docs: note Tab path autocomplete in README` or fold into previous commit if not yet pushed preference: separate small commit OK

---

## Self-review (plan)

1. Spec coverage: parse, Tab selection, Directory vs FileOrDirectory, Program wiring, tests for pure helpers — yes  
2. No placeholders  
3. Types consistent across tasks  
