---
name: voscript-lis
description: Scaffold the base VoiceOver PRO starter script set for a new LIS or AP system integration where reporting is the main surface - Epic Beaker, PowerPath, CoPath, Cerner Millennium, Meditech, SoftPath. Use when starting a new LIS integration, or when writing or fixing any LIS reporting command - CaseNumber, DictateSection, ReturnToWord, ReturnTo the LIS, NextCase, CaseComplete, SignoutReport, AddDeleteBlocks, OrderStains, ScanContainer. Covers the Core workflow every LIS gets, Word-as-editor LISs (NetOffice, the _Word library, protected report templates, section layouts) versus LISs with their own editor, desktop versus browser clients, chaining commands with *_Logic classes, the case status step that never signs a case out, and compile-checking a scaffold locally. For a slide viewer or image management system - Halo, AISight, Proscia Concentriq, Techcyte Fusion, Lumea BXLink, PathPresenter, Corista, Fuji, Gestalt PathFlow - use voscript-ims instead.
---

# VOScript Starter Scaffold — LIS / Reporting

Generates the Core script set for an LIS in `VOScript.Starter.{System}.Core`, built from the PowerPath Core rewrite
that was live-tested in `demo\sales` on 2026-09-28. For a slide viewer, use `voscript-ims`; both share everything in
`shared/references/`.

## What every LIS gets

Reporting is the spine, so the Core set is always built. Which scripts depends on **where the report is edited**:

| Script | Word editor (PowerPath, CoPath) | Page editor (the LIS's own) |
|---|---|---|
| `_CaseNumber` | accession formatter (`ISpeechFormatter`), per namespace | same |
| `_{System}` | the LIS library - every application-specific piece | same |
| `CaseNumber` | opens the spoken case, confirms it opened, starts dictation | same |
| `DictateSection` | opens Report Builder - from the LIS **or** the Word report | from the LIS |
| `ReturnToWord` | Report Builder into the Word report | - |
| `ReturnTo{System}` | saves Word back into the LIS + status step (omit with `-NoSaveToLis`) | writes the report fields |
| `NextCase` | saves and clears the case, ready for the next | same |
| `CaseComplete` | the returns, then NextCase, stopping at the first that didn't finish | same |

The commands hold the **workflow** and are the same for every LIS. Everything that depends on how *this* LIS is
driven - its windows, how it shows the open case, the keystrokes that open, save and clear one, how its Word report is
laid out - is in `_{System}`, marked `TODO:`. That is where the per-system work happens.

Common additions, per system, confirm before scaffolding: `SignoutReport` (manual sign-out only - see the rules),
`AmendReport`, `AddDeleteBlocks`, `OrderStains`, `ShowTab`, `ScanContainer`, `OpenTranscription`. They go in the
`.Ancillary` / `.Scanner` / `.Transcription` tiers.

## Namespace layout

```
VOScript.Starter.PowerPath._CaseNumber        libraries at the namespace root
VOScript.Starter.PowerPath._PowerPath
VOScript.Starter.PowerPath.Core.CaseNumber    the reporting spine
VOScript.Starter.PowerPath.Ancillary.OrderStains
```

| Tier | Holds |
|---|---|
| `.Core` | case open, dictate, the returns, next case, case complete |
| `.Ancillary` | blocks, stains, flags, notes, orders, status, printing, tabs |
| `.Recorder` / `.Scanner` (or `.Scanning`) / `.Transcription` | as named - match whichever spelling the namespace uses |

Shared libraries (`_Word`, `_Premium`, `_Common`, `_Browser`, `_Desktop`) sit directly under `VOScript.Starter`.

## Workflow

1. **Gather the inputs.** Ask only for what you cannot see:
   - System name, PascalCase (`PowerPath`) - the namespace segment. Vendor display name (`Sunquest PowerPath`).
   - **Editor: Word or the LIS's own page?** Decides the return commands.
   - **Word LIS: does it need a separate save-back?** PowerPath does (F10 from Word, then prompts); if not,
     `-NoSaveToLis`.
   - **Surface: desktop application or browser?** Decides how `_{System}` addresses the LIS.
   - The main window's title (desktop) or title fragment (browser); the return command's name if it isn't
     `ReturnTo{System}` (PowerPath's is `ReturnPowerPath`).

2. **Check the tenant's prerequisites.**

   ```bash
   pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/Test-Prerequisites.ps1" -IncludeReporting -IncludeWord -OutDir ./prereqs
   ```

   `-IncludeReporting` covers `_Premium` and `_Common` (which `_Premium` calls); `-IncludeWord` covers `_Word`. Add
   the browser core libraries' check as the IMS skill describes if `-Surface Browser`. Create anything `MISSING` or
   `INCOMPATIBLE` before generating; leave `DIFFERS-OK` alone.

3. **Generate.**

   ```bash
   pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/New-StarterScripts.ps1" -Kind Lis -System PowerPath -Vendor "Sunquest PowerPath" -WindowTitle "^PowerPath Client" -Editor Word -Surface Desktop -ReturnToName ReturnPowerPath -Author "you@voicebrook.com" -OutDir .
   ```

   | Parameter | Default | |
   |---|---|---|
   | `-Kind` | `Ims` | `Lis` for this skill |
   | `-Editor` | `Word` | `Word` or `Page` |
   | `-Surface` | `Desktop` | `Desktop` or `Browser` |
   | `-NoSaveToLis` | off | Word only: no `ReturnTo{System}` save-back |
   | `-Tier` | `Core` | the functional tier for the commands |
   | `-ReturnToName` | `ReturnTo{System}` | |
   | `-CaseTypeList` | `{System}CaseType` | Extended list; Target = Report Builder template |
   | `-ReportTemplate` | `Doc-{System}-Surgical` | fallback template and command document |

   Files land flat: `VOScript.Starter.{System}._{System}.cs`, `VOScript.Starter.{System}.Core.CaseNumber.cs`, ...,
   plus `{System}NamedLists.txt` (named lists, the palette items and the state properties the set reads).

4. **Compile-check locally before anything goes into a tenant.**

   ```bash
   pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/Test-Compile.ps1" -Path . -IncludePrerequisites
   ```

   It uses the PRO client's own compiler (`Client\roslyn\csc.exe`) and assemblies. PRO compiles a tenant's scripts
   as **one** assembly, so a script that does not compile blocks every script in that tenant, and the MCP cannot
   delete one. Run this again after every change to `_{System}`, and include any other tenant script the set calls
   (e.g. `DefaultCaseNumber`) by extracting it from the cache into the same folder.

5. **Resolve the TODOs in `_{System}` against the live application.** Read, don't drive:
   - Desktop: dump the LIS's windows and child controls read-only (class, instance, text, enabled) and the window
     titles as a case opens - `desktop-api.md` has the recipe and the Delphi quirks.
   - Word: dump the report's protection and Word sections before trusting the default layout - `word-editor.md`.
     The default is PowerPath's (plain-text headers); CoPath's protected headers need a different locator.
   - The status step: open the LIS's status dialog and read what it pre-fills before relying on `NextStatus`.

6. **Named lists and palette** from `{System}NamedLists.txt`. `DictateSection` has to be live in Microsoft Word as
   well as the LIS for a Word editor. Check every palette item points at the `.Core` class, not an older
   `VOScript.Standard` one.

7. **Deliver.** With the `voicebrook-pro` MCP connected, create or edit drafts directly (lock, re-read, edit,
   compile, unlock); the user publishes. Otherwise print each script inline in a fenced `csharp` block.

8. **Test live, with the log open.** The PRO client log (`VO_Logs\VO_ClientLog_{YYMMDD}.log`) shows every
   `(Script)` line the set writes - every early return in the Core set logs why, so "it didn't do anything" always
   has an answer there.

## Reference implementation

`${CLAUDE_PLUGIN_ROOT}/shared/examples/PowerPath.*.cs` - the tested PowerPath Core set: `_PowerPath` (window
patterns, the Word report layout), `_CaseNumber`, and `Core.CaseNumber`, `DictateSection`, `ReturnToWord`,
`ReturnPowerPath`, `NextCase`, `CaseComplete`. Read the matching one before changing a template or writing an
integration by hand.

References: `desktop-api.md`, `word-editor.md`, `script-catalog.md` (the LIS Core section), `conventions.md`,
`report-builder-parts.md`, and `browser-api.md` for a browser LIS.

## Rules that are not optional

- **Never automate sign-out.** No command sets a sign-out status or presses a sign-out button. The status step backs
  out of one - and treats that as success, so CaseComplete still moves on.
- **Every Core command is a `CommandScript` plus a `*_Logic` class returning `bool`**; return `false` only when the
  step did not finish, and log why on every early return.
- **Poll the real state; never a fixed wait for something that may not come, never a keystroke for a prompt that is
  not showing.**
- **Set `CaseNumber` and `CaseType` on every entry path** - spoken, from the LIS, from Word, from a scanner.
- **All Word work goes through `_Word.Edit`**, so a Word failure is logged instead of surfacing as a
  `SerializationException`.
- Keep every `#region` marker and the one-line `public class {ScriptName} : {BaseClass}`.
- Never guess a window title, control ID or keystroke into a shipped script. Leave the `TODO:` and say so.
- Finish or delete every generated script - they all compile against `_{System}`, so a half-customized library breaks
  the scaffolds left untouched. Compile-check after every library change.
