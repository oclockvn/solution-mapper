# Solution Mapper — Mapping Enhancements Design

Date: 2026-09-04
Source: brainstorming on progress tracking, automation, and reader robustness
Status: proposal (not yet planned or implemented)

## Context

The MVP (`2026-07-30-solution-mapper-design.md`) matches projects filename-first: a unique
legacy/upgraded pair for a `.csproj` name is auto-`Matched` at confidence `1.0`, and metadata
(`AssemblyName`, `TargetFramework`, `RootNamespace`, one shared `ProjectReference`) is only
scored inside the M×N duplicate tie-break. Every run rescans from scratch; `--export` writes a
one-shot snapshot that is never read back. Everything routes through Spectre prompts.

For a ~200-project migration run repeatedly over weeks, that leaves three gaps. This doc covers
three enhancements. Two earlier ideas — renamed-project matching, and solution-file awareness —
are deferred; see Non-goals.

Scope note: **`.csproj` only.** No `.fsproj` / `.vbproj` broadening.

Each section is independent and independently shippable. Suggested order: A → C → B
(reader hardening first because C's report and B's baseline diff both read better metadata).

---

## A. Harden the metadata reader

### Problem

`ProjectMetadataReader` does a raw `XDocument` walk of a single `.csproj`:

- **`Directory.Build.props` inheritance** is ignored, so a solution that sets
  `TargetFramework` / `RootNamespace` once at the repo root reads as empty on every project,
  weakening every metadata score and every baseline/report signal below.
- **MSBuild expressions** — `<AssemblyName>$(MSBuildProjectName)</AssemblyName>` or a TFM
  pulled from a property. Current code nulls a value if it `Contains("$(")`, which is a blunt
  guard for `AssemblyName` / `RootNamespace`; `TargetFramework` isn't guarded at all, and
  `$(...)` in a `ProjectReference` `Include` silently produces a broken path.

### Approach

**a. Resolve `Directory.Build.props` (lightweight, no MSBuild engine).**
For each project, walk parent directories up to the solution root collecting
`Directory.Build.props`. MSBuild imports the *closest* one, which imports its parent, so the
farther-up file's values are overridden by closer ones and finally by the project's own
`<PropertyGroup>`. Replicate that as a flat string merge of just the handful of properties we
care about (`AssemblyName`, `RootNamespace`, `TargetFramework(s)`). Ignore `Condition`,
`Choose`, targets files, and `Directory.Build.targets` (rarely holds identity props).

No `$(...)` evaluation — cheap and predictable. Cache the per-directory props read (same
`ConcurrentDictionary` pattern as `ProjectMetadataReader.Read`).

**b. Explicit expression handling.**
Replace the bare `string?` fields with a small result type instead of silent null:

```csharp
enum PropState { Literal, Unresolved, Absent }
readonly record struct PropValue(PropState State, string? Text);
```

- `AssemblyName` / `RootNamespace` / `TargetFramework` containing `$(` → `Unresolved`.
  `ProjectMatcher` treats `Unresolved` vs anything as *no signal* — not a match, not a
  conflict (today an unresolved value can't help; it must also never hurt).
- `ProjectReference Include` with `$(` → keep the raw string for display but mark it
  unresolved so `ProjectGraph.Resolve` skips it, reported alongside the existing
  "references that resolve outside the legacy root" list.

**c. `$(MSBuildProjectName)` special case.**
The single most common expression in `AssemblyName`. Resolve just that token to the project
filename-without-extension; leave everything else `Unresolved`. Cheap, high payoff.

### Files touched

- `Mapping/ProjectMetadataReader.cs` — `Directory.Build.props` walk + merge; `PropValue`.
- New `Mapping/DirectoryBuildProps.cs` — the up-walk + cache.
- `Mapping/ProjectMetadata.cs` — carry `PropValue`, not bare `string?`.
- `Mapping/ProjectMatcher.cs` — `Unresolved` / `Absent` contribute no score.
- `Mapping/ProjectGraph.cs` — skip unresolved `ProjectReference` includes explicitly.
- Tests: two-level `Directory.Build.props` (farther file supplies TFM, project overrides it);
  `$(MSBuildProjectName)` resolves; other `$(...)` stays `Unresolved` and scores neutral;
  unresolved `ProjectReference` is reported, not crashed.

### Risks

- Getting import/override order subtly wrong. Document the assumed order in the file; cover
  with the two-level test. It's a heuristic aid, not a build — wrong inheritance degrades a
  score, it doesn't break correctness.
- SDK-implicit props we still miss (`RootNamespace` defaulting to the project name when
  absent). Acceptable; best-effort. We could apply that same default ourselves if it proves
  useful.

---

## B. Persist and diff the mapping over time

### Problem

`--export` is write-only. A migration team runs the tool weekly and wants to see
*movement* — what got migrated since last week, what regressed — and wants manual
corrections ("these two folders are the same project, stop asking") to survive a rescan.

### B.1 Baseline diff

**Read back a prior export.** New flag `--baseline <path>` (or auto-load `mapping.json` from
CWD if present and `--no-baseline` not passed). Diff current mappings against the baseline:

- **Newly migrated** — was `LegacyOnly` in baseline, now `Matched`.
- **Regressed** — was `Matched`, now `LegacyOnly` / `Ambiguous`.
- **Newly ambiguous** — a duplicate appeared.
- **Resolved** — was `Ambiguous`, now cleanly `Matched`.

Print a "Since baseline (`<date>`)" block above the normal summary. In report mode
(section C) these become machine-readable counts.

The diff needs a **stable identity** for a mapping across runs — the same problem as
detecting an override pair (B.2). See "Pairing a mapping across runs" below; the baseline
diff uses option 1 or 2, an override needs option 3 or 4.

### B.2 Manual overrides that survive rescans

A file under `%APPDATA%\SolutionMapper\overrides\<roots-hash>.json` (mirrors how
`LastRootsStore` is keyed), or `--overrides <path>` to point elsewhere:

```jsonc
{
  "schemaVersion": 1,
  "roots": { "legacy": "C:\\Legacy", "upgraded": "C:\\Net10" },
  "overrides": [
    { "legacy":  "Services/Billing/Billing.csproj",
      "upgraded":"src/Components/Billing/Billing.csproj",
      "decision":"match",              // "match" | "reject"
      "note":"renamed in PR #212", "setBy":"quang", "setAt":"2026-09-04" }
  ]
}
```

Paths stored **relative to their root** so the file survives the tree moving to another
machine. Applied *after* automatic mapping, *before* the summary:

- `match` — force these two into a `Matched` node (`MatchConfidence` shown as "manual"),
  consume the now-superfluous `LegacyOnly` / `UpgradedOnly` entries.
- `reject` — split an auto-`Matched` pair back into two `*Only` nodes.

**Capture from the UI.** In the picker, a key (e.g. `o`) on the highlighted row opens a
prompt: pick a counterpart from the unmatched list to `match`, or `reject` the current
auto-match. Appended to the overrides file. This is the only *write* outside `%TEMP%` and
must be explicit — prompted, never silent.

---

## Pairing a mapping across runs / detecting an override pair

Both B.1 (is this the same mapping as last run?) and B.2 (does this stored override apply to
a mapping in this run?) need to decide *"these two records refer to the same project pair."*
The unit is a **(legacy `.csproj`, upgraded `.csproj`) pair** — or a half-pair for
`*Only` rows. Options, roughly weakest→strongest, and cheapest→most involved:

### Option 1 — Project name only

Key on `ProjectMapping.Name` (filename without extension).

- **Pro:** trivial; already unique for the common 1×1 case.
- **Con:** breaks on fan-out (`IsOneToMany` — one legacy, many upgraded copies share a
  name) and on M×N duplicate groups. Can't express "the `Billing` under `Services` vs the
  one under `Shared`."
- **Use for:** a first-cut baseline diff if fan-out is rare in the corpus. Not enough for
  overrides.

### Option 2 — Root-relative path pair

Key on `(RelPath(legacyRoot, LegacyProjectFile), RelPath(upgradedRoot, UpgradedProjectFile))`,
case-insensitive, `/`-normalized. For a `*Only` row, the present side + `null`.

- **Pro:** exact, human-readable, portable across machines, survives the tool version
  changing. Distinguishes duplicates. No metadata read needed.
- **Con:** a folder *move within the same tree* between runs looks like a
  delete + add (regressed + newly-migrated) even though it's the same project. For overrides
  that's arguably correct — the path you pinned no longer exists — but it's noisy for the
  baseline diff.
- **Use for:** the **default** for both baseline diff and overrides. Store both the legacy
  and upgraded rel-paths in the export and the overrides file.

### Option 3 — Content identity key

Derive a key from stable project *contents*: first non-empty of
`AssemblyName` (post-`Directory.Build.props`, non-expression) → `RootNamespace` → project
name; optionally combined with a sorted hash of `ProjectReference` names.

- **Pro:** follows a project through a folder move *and* a folder rename. `AssemblyName`
  rarely changes in a layout migration.
- **Con:** needs section A's reader to be solid. Not unique when `AssemblyName` is unset or
  shared (test projects, `.Tests` siblings). Two projects that legitimately share an
  assembly name collide.
- **Use for:** a *fallback* when the option-2 path key misses — "no exact path pair, but a
  project with the same `AssemblyName` and 3 of 4 shared refs is right here." Surface as a
  suggestion, don't auto-apply.

### Option 4 — Composite score with a threshold (fuzzy)

Score a stored/baseline record against every current mapping:

| Signal | Weight |
|---|---|
| Exact legacy rel-path match | +50 |
| Exact upgraded rel-path match | +50 |
| Same `AssemblyName` (resolved, non-empty) | +30 |
| Same project name | +20 |
| Same immediate parent folder name (either side) | +10 |
| Jaccard of `ProjectReference` name sets ≥ 0.5 | +10 |

Best current mapping over a threshold (start ~70) is "the same pair"; ties or
near-ties → report as ambiguous, apply nothing.

- **Pro:** degrades gracefully across simultaneous move + rename + ref churn. One mechanism
  serves baseline diff *and* override matching.
- **Con:** most code, needs tuning against a real corpus, and a wrong high-confidence match
  silently mis-applies an override (diffs the wrong folders). Weights are corpus-specific.
- **Use for:** later, if options 2+3 leave too many overrides stranded after a big
  restructure. Ship behind a `--fuzzy-baseline` flag first.

### Recommendation

Store **both root-relative path pairs** (option 2) in the export and overrides file from day
one — it's the cheapest exact key and everything else can be layered on later. Use option 2
for the baseline diff. For overrides, try option 2 exact first, then offer option 3 matches
as *suggestions the user confirms* (which just writes a fresh option-2 override). Defer
option 4 until a real run shows it's needed.

Guardrails regardless of option:

- Overrides file records the roots it was built against; on a roots-hash mismatch, **warn
  and skip**, never apply.
- An override whose stored path no longer exists on the relevant side → report it as
  "stale override" in the summary, don't silently drop it.
- `schemaVersion` on both files; refuse to read a newer version.

### Files touched

- `Mapping/MappingExport.cs` — `Read`; payload gains `generatedAt`, `schemaVersion`, and
  per-project `legacyRelPath` / `upgradedRelPath`.
- New `Mapping/MappingBaseline.cs` — `BaselineDelta` record + option-2 diff.
- New `Mapping/MappingOverrides.cs` — load / apply / append; option-2 exact match with
  option-3 suggestion fallback.
- `Program.cs` — `--baseline`, `--no-baseline`, `--overrides`; apply overrides before summary.
- `UI/MappingSummary.cs` — "Since baseline" + "stale overrides" blocks.
- `UI/ProjectPicker.cs` — the `o` action.
- Tests: each baseline transition; override `match` consumes both sides; override `reject`
  splits a pair; stale override reported; roots mismatch → skipped with warning; option-3
  suggestion offered when path moved.

---

## C. Non-interactive / CI mode

### Problem

Every path runs through Spectre prompts, so the tool can't gate a PR or feed a dashboard.
A migration team wants "fail the build if any project is unmigrated" plus a trend line.

### Approach

**a. `--report [path]`.** Runs scan → map → (overrides, baseline diff) and emits a report
instead of entering the picker. No prompts — missing roots are a hard error. No path → stdout;
path → JSON file there.

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-09-04T09:00:00Z",
  "roots": { "legacy": "...", "upgraded": "..." },
  "summary": { "matched": 178, "ambiguous": 4, "legacyOnly": 12,
               "upgradedOnly": 3, "manualOverrides": 5, "staleOverrides": 1 },
  "sinceBaseline": { "baselineDate": "2026-08-28",
                     "newlyMigrated": 8, "regressed": 1, "resolved": 2 },
  "durationsMs": { "scanLegacy": 210, "scanUpgraded": 190, "map": 340 },
  "unmigrated": [ { "name": "Billing.Legacy", "legacyRelPath": "Services/Billing/Billing.csproj" } ],
  "ambiguousProjects": [ /* name + candidate rel-paths */ ]
}
```

`durationsMs` comes from the existing `Metrics` infra.

**b. `--fail-on <conditions>`.** Comma list: `legacy-only`, `ambiguous`, `regressed`,
`stale-overrides`. Exit codes:

| Code | Meaning |
|---|---|
| 0 | Ran; no `--fail-on` condition met |
| 1 | Usage / roots error |
| 2 | Ran; a `--fail-on` condition was met |

CI: `mapper "$LEGACY" "$UPGRADED" --report report.json --baseline prev.json --fail-on legacy-only,regressed`.

**c. `--baseline` composes** — `--report --baseline prev.json` fills `sinceBaseline` and
enables `--fail-on regressed`.

**d. Quiet output.** In report mode suppress the Spectre spinner and banner (gate on the
flag, not `Console.IsOutputRedirected`, so behaviour is predictable). Human-readable summary
still goes to stderr.

### Files touched

- `Program.cs` — pull arg parsing into a small `CliOptions` type (it's unwieldy inline);
  branch to the report path before any prompt.
- New `Reporting/MappingReport.cs` — build + serialize.
- New `Reporting/FailConditions.cs` — evaluate `--fail-on`.
- `UI/Metrics.cs` — expose collected timings as data, not just `Dump`.
- Tests: `CliOptions` parsing (flags, missing values, bad combos); report contents;
  exit-code matrix per `--fail-on`; report mode emits no ANSI escape sequences.

### Risks

- Spectre writing ANSI to a redirected stream. Gate every `AnsiConsole` call in report mode;
  assert plain output in a test.
- Scope creep into a full dashboard. Ships JSON only; charting is the consumer's job.

---

## Performance

Section A needs metadata for **every** project, not just M×N groups.
`ProjectMapper.WarmMxNMetadata` already does a `Parallel.ForEach` pre-read for its subset —
generalize it to warm every discovered project file once, up front, before both the mapping
pass and `ProjectGraph.Build`. `ProjectMetadataReader.Cache` already dedupes, so the graph
build gets faster too (it currently re-reads). Net effect on a 200-project tree: one parallel
read pass instead of lazy re-reads. Keep the `Metrics.Measure` spans so a regression shows up
in `rtk gain` / the section-C report.

## Data model summary (after A + B + C)

```csharp
// MappingStatus unchanged: Matched, LegacyOnly, UpgradedOnly, Ambiguous

public sealed record ProjectMapping
{
    // ...existing...
    public string? LegacyRelPath { get; init; }    // NEW: root-relative, for cross-run identity
    public string? UpgradedRelPath { get; init; }   // NEW
    public string? MatchReason { get; init; }       // NEW: "manual", "auto", ...
    public bool IsManualOverride { get; init; }     // NEW
}
```

## Non-goals

- **Renamed-project matching** (auto-pairing `Billing.csproj` ↔ `Company.Billing.csproj`
  across a filename change). Dropped for now — the corpus keeps filenames stable, and the
  override mechanism in B covers the occasional exception by hand.
- **Solution-file (`.sln` / `.slnx`) awareness** — scoping to projects actually in a
  solution, comparing solution membership, using solution folders as a signal. The loose
  `.csproj` scan is good enough; `.slnx` parsing is its own project.
- **`.fsproj` / `.vbproj`** — `.csproj` only.
- **Full MSBuild evaluation** (`Microsoft.Build` packages) — the spec forbids the dependency;
  section A's string merge covers the common cases.
- Any write to the solution trees. Sections B and C write only to CWD / `%APPDATA%` / an
  explicit `--report` / `--overrides` path.
- A hosted dashboard or trend UI — section C emits JSON; visualization is downstream.

## Open questions

1. Overrides file location — `%APPDATA%` keyed by roots-hash (mirrors `LastRootsStore`, no
   litter) vs a file next to the roots (portable, discoverable, commit-able). Leaning
   `%APPDATA%` with `--overrides` to opt into a tree-local file.
2. Baseline diff on a **folder move within one tree** — treat as regressed + newly-migrated
   (option 2's honest answer) or suppress via an option-3 content-identity follow-up? Start
   with the honest answer; add the follow-up only if the noise is real.
3. `--fail-on stale-overrides` on by default in CI, or opt-in? Probably opt-in.
