---
name: voscript-ims
description: Scaffold the base VoiceOver PRO starter script set for a new IMS or digital pathology slide viewer integration - Halo, AISight, Proscia Concentriq, Techcyte Fusion, PathPresenter, Lumea BXLink, Corista DP3, Fuji Synapse, Gestalt PathFlow. Use when starting a new image management system or viewer integration, when asked to "scaffold", "stub out" or "create the base scripts" for a pathology viewer, or when writing any individual viewer command script - CaseNumber, NavigateSlides, NavigateCases, ReturnToWorklist, RotateSlide, SetMagnificationLevel, SlideMarkup, ShowTab, PanSlide, PullBiomarkerResults. Covers slide navigation, magnification, annotations, AI biomarker resulting, and the optional reporting workflow an IMS exposes when the site has an IMS-to-LIS interface. Also use when asked how the VOScript.Starter house style, the _Browser helper library, or the Report Builder handoff works. For an LIS where reporting is the whole point - Epic Beaker, PowerPath, CoPath - use voscript-lis instead.
---

# VOScript Starter Scaffold — IMS / Slide Viewers

Generates the base `VOScript.Starter.{System}` script set for a new image management system or
slide viewer, in the house style used by `VOScript.Starter.Halo`, `.AISight` and `.Concentriq`.

For an LIS where reporting is the main surface rather than the viewer, use the `voscript-lis`
skill in this plugin. The two share every convention in `shared/references/` — only the script
catalog and the automation surface differ.

## What the base set is

Every integration gets these, assuming the system supports the functionality:

| Script | Type | Purpose |
|---|---|---|
| `_CaseNumber` | ExtensionScript, `ISpeechFormatter` | Builds the accession number from spoken parameters. Format differs per system, so every namespace carries its own. |
| `_{System}` | ExtensionScript | Extension library: window/title discovery and any system-specific helpers (deep links, panel anchors). |
| `CaseNumber` | CommandScript | Opens a case from the worklist. Optionally starts Report Builder. |
| `NavigateCases` | CommandScript | Next/previous case, via the viewer's own case control. |
| `ReturnToWorklist` | CommandScript | Leaves the open case and returns to the worklist. |
| `NavigateSlides` | CommandScript | Next/previous slide in the viewer. |
| `RotateSlide` | CommandScript | Sets slide rotation to a spoken value. |
| `SetMagnificationLevel` | CommandScript | Sets viewer zoom from a spoken named list. |
| `ShowTab` | CommandScript | Opens a named panel/tab in the viewer's tool tray. |
| `SlideMarkup` | CommandScript | Activates an annotation tool by name. |

`CaseNumber` and `SlideMarkup` exist in every shipped integration; the rest depend on what the
viewer offers. `PanSlide` is common enough to consider part of the base set (6 of 9 systems).

**Never scaffold `NextCase` here.** It is an LIS command — close the case and reset the LIS for the
next search. Several shipped IMS namespaces use the name anyway for what is really `NavigateCases`
or `ReturnToWorklist`; `script-catalog.md` lists which is which. Do not infer behaviour from that
name when reading existing code.

### The optional reporting workflow

Most of these systems expose report elements, but **whether a site uses them depends on whether an
interface has been built between the IMS and the LIS at that customer.** An IMS with reporting
scripts in the codebase is not the same as an IMS that reports at this site.

So this is not a property of the integration you can look up — **ask.** With no IMS-to-LIS
interface the reporting surface stays in the LIS, the IMS scripts are viewer-only, and scaffolding
the reporting set produces commands nobody can use.

Add when the site reports out of the IMS:

| Script | Purpose |
|---|---|
| `DictateSection` | Reads the case number off the open case and initializes Report Builder for it. |
| `ReturnTo{System}` | Writes Report Builder text back into the IMS report fields (e.g. `ReturnToHaloAP`). Spoken phrase is always `send report`. |
| `InsertChecklist` | Inserts a CAP checklist into the active document. |
| `SendChecklist` | Pushes completed checklist content back to the IMS. |

Per-system extras in this tier, worth checking for rather than assuming: sign-out
(`PathFlow.SignoutCase` / `SaveCase`, `AISight.FinalizeCase`), order entry (`Fusion.OrderStain`,
`BXLink.OrderStain` / `OrderTest`) and coding (`PathPresenter.AddICDCode` / `AddCPTCode`).

Add when the system has **AI resulting**:

| Script | Purpose |
|---|---|
| `PullBiomarkerResults` | Entry point: detect CAP template, resolve mapping + label overlay, run `_CapEngine`. |
| `_{System}LabelRegistry` | Maps CAP PartKey to the vendor's label overlay builder. |
| `{Namespace}.Labels._{System}{Template}Labels` | The overlay itself: CAP field to vendor on-screen label. |
| `_{System}Adapter` | Implements `IAiResultSource.ReadValueByLabel`. Scrapes the vendor UI. |

## Workflow

1. **Gather the inputs.** Ask only for what you cannot see:
   - System name in PascalCase (`Halo`, `AISight`, `Concentriq`) — becomes the namespace segment.
     Use the **product** name, not the vendor's: the palette is `Proscia Concentriq`, the namespace
     is `Concentriq`.
   - Vendor display name for log messages (`Indica Labs Halo AP`).
   - Browser window title fragment the viewer shows (`Halo AP`, `AISight`, `Concentriq AP`).
   - **Whether this site has an IMS-to-LIS interface** — this decides whether the reporting set is
     scaffolded at all. Ask explicitly; it is a per-customer deployment fact, not a property of the
     product.
   - Whether it has AI resulting.
   - Output directory (default: the current project folder).

2. **Check the tenant's prerequisites first.** Generated scripts call `_Browser` and
   `Browser.Manager.BrowserManager`; nothing compiles without them, and **not every tenant has
   them.** A fresh QA or customer tenant often has neither.

   ```bash
   pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/Test-Prerequisites.ps1" -OutDir ./prereqs
   ```

   Add `-IncludeReporting` when using `-Reporting` (pulls in `_Premium`) and `-IncludeAi` when
   using `-AiResulting` (the `CapResultParser` libraries). `-OutDir` stages the source of anything
   missing, from the bundled copies in `${CLAUDE_PLUGIN_ROOT}/shared/prerequisites/`, ready to
   create in PRO. Create those before generating anything, and note that `BrowserManager` declares
   `IDisposable` rather than `ExtensionScript`.

   **Revision numbers are not compared, and must not be.** PRO configuration is not centralized —
   a tenant is a starter export plus deltas from whichever resource's configuration — so `@1P` in
   one tenant is not the same code as `@1P` in another, and higher is not newer. The check
   compares declared members against what the templates actually call.

   `DIFFERS-OK` means the tenant's code differs but provides everything needed: **leave it alone**,
   since overwriting it could break scripts already in that tenant. Only `MISSING` and
   `INCOMPATIBLE` need action, and `INCOMPATIBLE` lists exactly which members are absent.

3. **Generate.** Run the scaffolder:

   ```bash
   pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/New-StarterScripts.ps1" -System Halo -Vendor "Indica Labs Halo AP" -WindowTitle "Halo AP" -ReturnToName ReturnToHaloAP -Author "you@voicebrook.com" -OutDir . -Reporting -AiResulting
   ```

   Placeholders in this file are written `{System}`; the template files use `{{System}}`, which is
   what the scaffolder actually substitutes.

   | Parameter | Default | |
   |---|---|---|
   | `-System` | required | PascalCase; becomes `VOScript.Starter.{System}` |
   | `-Vendor` | `{System}` | Display name used in log messages and help text |
   | `-WindowTitle` | `{System}` | Browser window title fragment |
   | `-CaseTypeList` | `{System}CaseType` | Named list holding the case types |
   | `-ReportTemplate` | `Doc-{System}-Surgical` | Fallback Report Builder template |
   | `-ReturnToName` | `ReturnTo{System}` | The command is usually named for the product, not the namespace — `ReturnToHaloAP` |
   | `-Reporting` | off | Adds `DictateSection` and the return command |
   | `-AiResulting` | off | Adds `PullBiomarkerResults`, the registry, a label overlay, the adapter |
   | `-Force` | off | Overwrite files that already exist |

   Files land as `VOScript.Starter.{System}.{ScriptName}.cs`, flat, matching the naming
   convention already used in these project folders.

4. **Dump the UIA tree before resolving anything.** Run `_Browser.DumpTree` once on every surface
   the scripts touch — worklist, case detail, viewer, and each menu or panel that has to be open
   — and keep the output to hand. This is mandatory, not advisory. Claude in Chrome is for
   reconnaissance only: it finds candidate names fast, but it reports the DOM, and three things
   the scripts depend on are either absent from it or shown differently:

   - **ControlType.** A rotation input can be a `Spinner`, not an `Edit`.
   - **Accessible name.** An icon control's Material ligature appears as a *child* in the browser
     tree but as the element's *own* Name in UIA.
   - **Interaction.** Whether `SetElementText` works, or the field has to be typed into.

   Where the browser and UIA disagree, UIA is the answer. Resolve every `TODO:` against the dump,
   then re-read the gotchas in `${CLAUDE_PLUGIN_ROOT}/shared/references/browser-api.md` against what you just wrote —
   `IsVisiblyRendered` vs a null check is the one most often missed. Do not expect another
   integration against the same vendor to check your work; there usually isn't one.

5. **Read `${CLAUDE_PLUGIN_ROOT}/shared/references/script-catalog.md`** for each script's known-good variants, and fill in the
   `TODO:` markers. Each generated script compiles as written, but every element name, keystroke
   and anchor in it is a placeholder.

6. **Deliver the script text inline in the response**, not just a file link. These get pasted
   into VO_ScriptEdit by hand; a path alone is not usable. Print each script in a fenced
   `csharp` block, one per script.

7. **Named lists.** The scaffolder also writes `{System}NamedLists.txt` — a copy/paste
   worksheet of every list the generated commands reference, in `Text\Spoken form` format, with
   the magnifications pre-filled and everything else marked `TODO`. Fill it in from the live
   application alongside the scripts, delete the lists the system doesn't support, and hand it
   over with the code. Spoken forms follow the Dragon rules restated at the bottom of the file:
   no punctuation, numbers as words, acronyms spaced and capitalised, pipe-separated
   alternatives.

8. **Command palette.** Each `CommandScript` needs a palette entry with its trigger and
   properties. See `${CLAUDE_PLUGIN_ROOT}/shared/references/conventions.md` for the palette XML shape and the standard
   triggers for each command.

## Where the real examples live

**Start with the examples bundled in this plugin** — `${CLAUDE_PLUGIN_ROOT}/shared/examples/`,
whose `README.md` maps each file to the shape it demonstrates. Fifteen real published scripts, plus two newer tested Lumea scripts,
covering every addressing idiom, the Report Builder handoff and the return path. They are always
present, so nothing depends on what a given machine has cached.

That matters because **not every tenant carries every integration.** A QA or customer tenant may
hold almost none of them, and a pinned revision number rots — the library revision differs per
tenant and moves.

**If the `voicebrook-pro` MCP is connected, prefer it over the cache.** It reads the same tenant
the client is signed into, live, with no path or registry work: `pro_script_list` /
`pro_script_search` to find, `pro_script_get` for full source and declared properties, and
`pro_config_get` on `R_CommandPalette` for the **real triggers** — the only reliable way to learn
which named list a command actually uses. `pro_config_get` on `R_NamedListSetup` / `GlobalSetup`
gives every list with its `listType`. Reads need no lock. One caveat: `pro_script_list` reports
`customScriptStatus: uncompiled` for everything, so take the real status from `pro_script_get`.

The MCP and the cache read the *same* tenant, so both carry the tenant caveat below equally.

The local cache is the fallback, and the *newer* source when it happens to hold what you need.
Everything below is about reading it safely.

Every published script for a given tenant is at
`{ProgramData}\{ClientEnvironment}\{tenant}\VO_LocalCache\R_ScriptSource`.

**Resolve it from the registry, not by guessing or scanning.** `HKLM:\SOFTWARE\Voicebrook\PRO_Client`
holds the environment the client is currently pointed at:

```powershell
$p = Get-ItemProperty "HKLM:\SOFTWARE\Voicebrook\PRO_Client"
$tenants = $p.AvailableTenants -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$p.ProgramData        # C:\ProgramData\Voicebrook\PRO_Client
$p.ClientEnvironment  # demo | pro | qa | staging
$tenants              # AvailableTenants is PIPE-SEPARATED and may hold several
```

Then:

- **One tenant** — build the path and use it, no question needed.
- **Several tenants** — list them and ask which. Never pick one silently.
- **Key missing** — fall back to scanning `{ProgramData}` for `R_ScriptSource` directories, and
  ask, because a machine typically holds many.

A single machine commonly caches ten or more tenants, most of them live customer configs under
`pro`. Do not treat script count as a tiebreak: a customer tenant can carry hundreds of Starter
scripts and still be the wrong reference, because a production config is site-specific — that
site's local modifications, templates and named lists rather than the house pattern. Reading one
as the house style inherits its workarounds and risks site-specific strings reaching generated
scripts. Reading a customer tenant should be a deliberate answer to the question, never a default.

Files are named `{ExportKey}@{revision}{stage}-{id}-{n}.xml`, stage `P` = Published, `A` = Active,
`D` = Draft. **Read the highest-numbered `P` revision** — lower revisions are superseded and `A`/`D`
are work in progress. The source is the XML-escaped `ScriptCode` attribute:

```powershell
[xml]$x = Get-Content -Raw $scriptPath; $x.RC_ScriptSource.ScriptCode
```

Consult it before writing anything non-obvious. `Halo`, `AISight`, `Fusion`, `BXLink`,
`Concentriq`, `Corista`, `Fuji`, `PathFlow` and `PathPresenter` each carry a full or partial
base set, and comparing two or three of them shows which parts of a pattern are universal and
which are that vendor's quirk. The helper library is `VOScript.Starter._Browser` - read the highest `P` revision in whichever tenant you are looking at. Do not rely on a pinned revision number; it differs per tenant and moves.

`Halo` is the most recent and cleanest of these, so prefer it as the reference for structure. One
thing not to carry across: its `_CaseNumber` hardcodes the year (`"-23-"`) as a sandbox convenience
so the year need not be spoken against test cases. Fine there, must be replaced with the derived
year before distribution.

What the cache is good for is **patterns** — how a return-to-IMS is structured, which addressing
shapes exist, what the house style looks like. It cannot tell you this vendor's control types or
element names, and for a genuinely new system there is no sibling integration to check against.
That is what step 3 is for.

## Reference material

- `${CLAUDE_PLUGIN_ROOT}/shared/references/conventions.md` — boilerplate anatomy, property attributes, state properties,
  named lists, palette entries, file naming.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/browser-api.md` — `_Browser` / `_Window` / `BrowserManager` helper surface, and
  the gotchas that have cost real debugging time.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/script-catalog.md` — per-script intent, the known-good variants of each pattern,
  and what has to be verified per system.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/ai-resulting.md` — the CAP biomarker pipeline and how a vendor plugs into it.
- `${CLAUDE_PLUGIN_ROOT}/shared/references/report-builder-parts.md` — working inside the open Report Builder document:
  lists, adding specimens, PartKey matching, picklist fields, the cursor, and filling the gross from
  a device.

## Rules that are not optional

- Keep every `#region` marker exactly as generated. VO_ScriptEdit parses `PROPERTYDEF` and
  `CLASSDEF` regions; reformatting them breaks the property editor.
- Keep `public class {ScriptName} : {BaseClass}` on one line, and the class name identical to
  the script name.
- Every browser script disposes its controller in a `finally`.
- Errors go to `StatusLog.WriteErrorEntry` / `WriteWarningEntry` — never a silent catch.
- Never guess an element name into a shipped script. Leave the `TODO:` and say so.
- **Finish or delete every generated script — never leave one as raw scaffold.** All of them
  compile against the same `_{System}` library, so the moment you customize that library the
  untouched scaffolds break. Removing `CaseNumberTitlePattern` from `_Concentriq` (right call: the
  Concentriq title never carries the case number) left the unresolved `NavigateCases` calling a
  member that no longer existed, and it failed to compile on sync. When a system genuinely lacks
  a capability, say so and **delete the file** — do not hand over a scaffold as a placeholder.
  Before delivering, grep the generated set for every member you changed or removed.
