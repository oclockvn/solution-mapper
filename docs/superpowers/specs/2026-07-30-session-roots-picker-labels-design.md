# Solution Mapper — Session Roots + Picker Labels

Date: 2026-07-30  
Parent: `docs/superpowers/specs/2026-07-30-solution-mapper-design.md`

## Goal

Fix two interactive UX gaps:

1. **Remember last solution roots** and offer reuse on startup (always rescan).
2. **Show distinguishable picker lines** — project name, status, and short relative paths so duplicate names are identifiable.

## Decisions

| Topic | Choice |
|---|---|
| Last compare | Remember legacy + upgraded **solution roots** only; Confirm reuse; then rescan |
| Autocomplete | Out of scope this round |
| Picker label | One line: name + status + short relative path(s) |
| Persistence | `%LOCALAPPDATA%\SolutionMapper\last-roots.json` |

## Last roots

### Storage

File: `%LOCALAPPDATA%\SolutionMapper\last-roots.json`

```json
{
  "legacyRoot": "D:\\src\\legacy",
  "upgradedRoot": "D:\\src\\net10"
}
```

Helper (e.g. `UI/LastRootsStore.cs` or `Session/LastRootsStore.cs`):

- `TryLoad()` → `(legacy, upgraded)?` or null if missing, corrupt, or either directory no longer exists
- `Save(legacyRoot, upgradedRoot)` after roots are validated

### Flow

```text
CLI both roots?
  yes → use CLI paths
  no / incomplete →
      TryLoad ok?
        yes → show paths → Confirm "Reuse these roots?" (default yes)
             → no → prompt as today
        no → prompt as today
→ validate dirs → Save → scan/map (always fresh)
```

- Export JSON remains optional and is **not** the session source of truth.
- CLI-supplied roots still update `last-roots.json` after validation.

## Picker labels

`ProjectPicker.Pick` takes `legacyRoot` and `upgradedRoot`.

One-line format (no Spectre `[` `]` in the label text):

```text
{Name}  {status?}  {pathHint}
```

Examples:

```text
Accounts  (ambiguous)  L:Services\Accounts → U:src\Accounts
Accounts  upgraded only  U:src\Foo\Accounts
Accounts.Tests  L:Tests\Accounts.Tests → U:test\Accounts.Tests
Billing
```

Rules:

- Prefer `Path.GetRelativePath(root, folder)`; if not under root, show last 2–3 path segments
- Matched / Ambiguous with both folders: `L:{rel} → U:{rel}`
- Legacy-only: `L:{rel}` with status `legacy only`
- Upgraded-only: `U:{rel}` with status `upgraded only`
- Clean matched (not ambiguous): name + path hint; status text optional/omitted when matched and unambiguous
- Search filter matches **name or** path hint / folder paths (case-insensitive)

## Non-goals

- Path autocomplete / OS folder dialog
- Remembering selected project names
- Manual path override editor for pairs
- Changing matching/scoring algorithm
- Treating `--export` mapping as session state

## Testing

- `LastRootsStore`: round-trip save/load; missing → null; deleted dir → null
- Format helper (pure): matched / one-sided / relative path cases; no `[` `]` in output

## Success

1. Run `mapper` with no args twice: second run offers last roots; after reuse, scan runs fresh.
2. Picker with duplicate `Accounts` / `Accounts.Models` entries shows different relative paths so each row is identifiable.
