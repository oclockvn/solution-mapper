# Path Tab Autocomplete — Design

Date: 2026-07-30  
Parent: interactive roots / export prompts in Solution Mapper

## Goal

When the user enters a filesystem path interactively, **Tab** shows matching child paths to select (dropdown / selection list), then continues editing. Shared helper for all path prompts.

## Decisions

| Topic | Choice |
|---|---|
| Scope | All path prompts via shared helper |
| Entry kinds | Mode flag: `Directory` vs `FileOrDirectory` |
| Interaction | Custom key loop + Spectre `SelectionPrompt` on Tab (Approach 1) |
| Match | Case-insensitive prefix on last path segment |
| OS folder dialog | Out of scope |

## API

New file e.g. `SolutionMapper/UI/PathAutocompletePrompt.cs`:

```csharp
public enum PathKind
{
    Directory,
    FileOrDirectory
}

public static class PathAutocompletePrompt
{
    public static string Prompt(
        string title,
        PathKind kind,
        Func<string, ValidationResult>? validate = null);
}
```

Wiring:

- Legacy / upgraded roots → `PathKind.Directory` + must exist as directory  
- Export path → `PathKind.FileOrDirectory` (user may pick folder then type filename, or pick existing file)  
- Replace `PromptExistingDirectory` in `Program.cs`

## Match rules (Tab)

Parse current buffer into **parent directory** + **prefix** (last segment):

| Buffer | Parent | Prefix | Suggestions |
|---|---|---|---|
| `D:\` | `D:\` | `""` | All children of `D:\` |
| `D:\p` | `D:\` | `p` | Children of `D:\` starting with `p` |
| `D:\proj\` | `D:\proj\` | `""` | All children of `D:\proj\` |
| empty / no root yet | — | — | Logical drives (`GetLogicalDrives()`) |

- `Directory`: folders only  
- `FileOrDirectory`: folders and files  
- Case-insensitive prefix match  
- Inaccessible directories: skip; do not crash  
- Cap list (~50); if truncated, note in prompt title  

## Key / UI flow

```text
show title + current buffer
loop:
  printable → append to buffer
  Backspace → delete last char
  Tab →
    0 matches → message "no matches"
    1 match → append that name; if folder, append trailing separator
    N matches → SelectionPrompt → append choice; folder → trailing separator
  Enter → validate → return Path.GetFullPath(buffer) or show error and continue
  Esc / Ctrl+C → abort (same spirit as cancel interactive setup)
```

ASCII-only labels (`->` style already used elsewhere). After selecting a folder, buffer ends with `\` so the next Tab lists that folder’s children.

## Non-goals

- Windows folder-picker dialog  
- Fuzzy / substring match beyond prefix  
- Special UNC handling beyond best-effort `Directory.Enumerate*`  
- Spectre e2e keyboard automation tests  

## Testing

Pure unit tests for path parse + filter:

- `D:\`, `D:\p`, `D:\proj\` → parent/prefix  
- Prefix filter against a fake child name list  
- Drive-root suggestion when buffer empty  

No full console key-loop e2e.

## Success

Interactive:

```text
Legacy solution root:
> D:\<Tab>     → list children of D:\
> D:\p<Tab>    → list children starting with p
```

Select folder, continue typing, Enter validates and proceeds. Same helper used for upgraded root and export path (file-or-directory mode).
