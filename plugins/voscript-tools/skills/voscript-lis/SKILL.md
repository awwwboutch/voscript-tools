---
name: voscript-lis
description: STUB - not yet exercised on a real integration; the script catalog in it is a placeholder. Scaffold the base VoiceOver PRO starter script set for a new LIS or AP system integration where reporting is the main surface - Epic Beaker, PowerPath, CoPath, PathFlow, Orchard, NovoPath. Use when starting a new LIS integration, or when writing any individual reporting command script - DictateSection, ReturnTo the LIS, NextCase, SignoutReport, CaseComplete, AddDeleteBlocks, OrderStains, InsertChecklist. Covers the Report Builder handoff, section-to-field mapping, sign-out workflow, and desktop as well as browser automation. For a slide viewer or image management system - Halo, AISight, Concentriq - use voscript-ims instead.
---

# VOScript Starter Scaffold — LIS / Reporting

> **STATUS: STUB.** The script catalog below is a placeholder drawn from the shipped Epic,
> PowerPath, CoPath and PathFlow namespaces. Nothing here has been through a real scaffolding run
> yet. Fill it in on the first LIS job rather than trusting it as written, and delete this notice
> once it has been exercised end to end.

For a slide viewer where the imaging surface is the point, use the `voscript-ims` skill in this
plugin instead. Everything in `shared/references/` applies to both — the boilerplate and regions,
`_CaseNumber`, named lists and the Dragon rules, the palette, and the Report Builder handoff. What
differs is the script catalog and, for a desktop LIS, the automation library.

## What the base set looks like

Reporting is the spine here rather than an add-on, so `DictateSection` and the return command are
core, not optional.

| Script | Type | Purpose |
|---|---|---|
| `_CaseNumber` | ExtensionScript, `ISpeechFormatter` | Builds the accession number from spoken parameters. |
| `_{System}` | ExtensionScript | Window/activity discovery and system-specific helpers. |
| `CaseNumber` | CommandScript | Opens the spoken case. |
| `DictateSection` | CommandScript | Initializes Report Builder for the open case. |
| `ReturnTo{System}` | CommandScript | Writes Report Builder text back into the LIS report fields. |
| `NextCase` | CommandScript | Advances to the next case in the worklist. |
| `SignoutReport` | CommandScript | Signs the case out. |

Common additions, per system — confirm which exist before scaffolding any of them:
`CaseComplete`, `AmendReport`, `AddDeleteBlocks`, `OrderStains`, `InsertChecklist`,
`SwitchActivity`, `OpenTranscription`, `ScanBlock` / `ScanContainer`.

## Before this skill is usable

1. **Decide the automation surface.** A web LIS uses `_Browser` and `BrowserManager` exactly as
   the IMS skill does. A desktop LIS (PowerPath, CoPath) uses `VOScript.Starter._Desktop` and
   window handles instead. The script *set* is the same either way; only the addressing library
   changes. A `shared/references/desktop-api.md` needs writing to sit alongside `browser-api.md`
   before the first desktop job — read `VOScript.Starter._Desktop` from the PRO cache to build it.

2. **Build the template set.** `shared/templates/` currently holds the IMS catalog. The reporting
   templates that already exist there — `DictateSection.cs`, `ReturnToSystem.cs`, `_CaseNumber.cs`,
   `_System.cs`, `CaseNumber.cs` — carry over directly. `NextCase`, `SignoutReport` and the rest
   need writing, grounded in the shipped Epic / PowerPath / CoPath / PathFlow scripts rather than
   invented.

3. **Teach the scaffolder the second catalog.** `New-StarterScripts.ps1` currently emits the IMS
   set. It needs a switch selecting which template set to use; token substitution, file naming and
   the named-list worksheet are identical and should not be duplicated.

## Everything else

Until this skill is filled in, follow `voscript-ims` for process — the workflow, the mandatory
`_Browser.DumpTree` pass, the delivery rules and the named-list worksheet are the same. The
reference material is shared:

- `${CLAUDE_PLUGIN_ROOT}/shared/references/conventions.md` — boilerplate, properties, state
  properties, named lists and the Dragon spoken-form rules, palette entries, Report Builder handoff.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/browser-api.md` — `_Browser` / `_Window` /
  `BrowserManager`, the addressing idioms, and the gotchas.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/script-catalog.md` — per-script intent and variants.
  IMS-focused today; LIS entries belong here too.
- `${CLAUDE_PLUGIN_ROOT}/shared/examples/` — fifteen real published scripts, always present
  regardless of what the local machine has cached. `AISight.DictateSection.cs` and
  `AISight.ReturnToAISight.cs` are the two that matter most here: they are the Report Builder
  handoff and the return path, which are the spine of any LIS integration. Read those before
  writing either.

## Rules that are not optional

Identical to `voscript-ims`, and they apply from the first LIS script written:

- Keep every `#region` marker exactly as generated.
- Keep `public class {ScriptName} : {BaseClass}` on one line, class name identical to script name.
- Every browser script disposes its controller.
- Errors go to `StatusLog.WriteErrorEntry` / `WriteWarningEntry` — never a silent catch.
- Never guess an element name into a shipped script. Leave the `TODO:` and say so.
- Finish or delete every generated script — never leave one as raw scaffold. They all compile
  against the same `_{System}` library, so customizing it breaks any scaffold left untouched.
