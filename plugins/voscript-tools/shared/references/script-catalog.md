# Script catalog

What each script is for, which variants exist in the shipped integrations, and what has to be
confirmed against the live application before it works.

---

## `_CaseNumber` — accession formatter

`ExtensionScript, ISpeechFormatter`. Assembles the accession number from the spoken case type,
year and digits. Every namespace carries its own because the format differs per system.

**Confirm:** the exact assembled shape. Real formats in use:

| System | Shape | Example |
|---|---|---|
| Halo | `<Type><YY>-<counter>` | `25GP-19429` |
| AISight | `<Type>-<YY>-<counter>` | `SP-26-00123` |
| FlexLIS | `<Type><YY>-<counter>` | `SN26-00123` |

**Do not repeat these bugs:**
- Reading `Property("LeadingZeroes", ...)` against a declared `LeadingZeros`. Silently returns
  the default forever.
- Building the zero pad with `new string('0', counterLength - shortNum.Length)`. Throws
  `ArgumentOutOfRangeException` the moment more digits are spoken than `CounterLength`. Use
  `PadLeft`.
- Hardcoding the year (`caseType + "-" + "23" + "-" + fullNum` shipped in one namespace).

---

## `_<System>` — extension library

Window and title discovery, plus anything more than one command needs. Two title helpers, and
picking the wrong one is a recurring bug — see `browser-api.md`.

Systems reached by URL rather than by clicking the worklist also put their deep-link builders and
an `EnsureWindow` helper here. `_Flexlis.cs` is the model: one reused window per surface
(Pathologist Review, Slide Viewer), each pinned to a configured monitor.

---

## `CaseNumber` — open a case from the worklist

Two shapes:

1. **Worklist search** (Halo, AISight, the generated template). Set the filter column, type the
   accession, wait for the row, click the link. Works everywhere; depends on element names.
2. **Deep link** (FlexLIS). Build the URL for each surface and navigate a reused window to it.
   Much more robust when the vendor has stable routes — no dependence on column order or on the
   viewer link being at a fixed child index. Prefer this when the vendor exposes routes.

Both set `CaseNumber` as a state property *first*, then optionally start Report Builder.

**Confirm:** the filter control and its accession option, the search field's name, whether the
result link is a `Hyperlink` or a `Button`, and whether the click opens the case in the same
window or a new one.

---

## `NavigateCases` — next/previous case

Presses the next-case or previous-case control in the **slide viewer**, not the worklist. Runs
against an open case, so it needs `FindCurrentTitlePageByRegex`.

The proven shape (Halo) is the caption-Text idiom: find `"Next Case"` / `"Previous Case"` as
`Text`, click the parent. Search for a `Button` by that name and you get null.

Not every viewer has these controls. If this one does not, say so and drop the script rather than
shipping something that half-works.

---

## `NavigateSlides` — next/previous slide

A keystroke, and usually nothing else. `PageDown`/`PageUp` in Halo and AISight; `Down`/`Up` in
FlexLIS, which also needs the image library panel open first.

**Call `WindowTools.EnsureForegroundWindow(windowCaption)` before the keypress.** Without it the
command drives whatever the pathologist last clicked. Halo's version is six lines and never opens
a `BrowserManager` at all.

---

## `RotateSlide` — set rotation

Two shapes, and which one applies depends entirely on what the viewer offers:

1. **The gadget has a rotation input.** Try `SetElementText` first, and only fall back to
   clicking into the field and typing if it does not take. Both are in production: Concentriq's
   input is a `Spinner` whose accessible name is the degree symbol `"°"` and it accepts
   `SetElementText` directly, while AISight's exposes no `ValuePattern` and has to be typed into
   and committed with Enter. Do not assume the AISight shape — it cost a rewrite once already.
2. **Only incremental hotkeys** (Halo: `O` and `P`, 5° per press). Read the *current* rotation off
   the gadget's caption (`subGroup.CurrentName.Replace("°", "")`), compute the delta, and press
   the key `delta / 5` times. This lands on the nearest 5° step, not the exact value. If the
   viewer wraps at 360, normalize with `((desired - current + 540) % 360) - 180` so a 10°
   move never becomes a 350° one.

Either way the input is reached by anchoring off a named neighbour. Comment what the hops mean.

---

## `SetMagnificationLevel` — zoom

Named list → toolbar click, or named list → keystroke.

The toolbar route is the caption-Text idiom again: find the level's caption as `Text`, click the
parent. The keystroke route (`1`–`6` for Fit/2x/4x/10x/20x/40x in AISight) is faster and does not
need the toolbar rendered, but the viewer must be foregrounded first.

Whichever route, fail loudly on an unmapped level. A silent no-op is indistinguishable from a
broken microphone to the pathologist.

---

## `ShowTab` — bring a named tab into focus

The intent is just that: focus the spoken tab, however the system happens to expose it. Three
production shapes, in order of how much the vendor cooperates:

1. **Named tabs and buttons in one tray** (Fusion). Decide the ControlType per tab name — real
   `TabItem`s get `_Browser.FocusTabItem`, toolbar toggles are `Button`s and get clicked. This
   is the general case and what the template leads with.
2. **The tab only exists while its panel is open** (BXLink). `FindElementOnPage` returns null
   because the panel is collapsed, not because the tab is missing. Open the panel first, then
   re-find. Check `IsTabSelected` before focusing, so an already-selected tab is not toggled off.
3. **Icon-only tray with no usable names** (Halo). The named list translates the spoken tab to a
   sibling *offset* from a named anchor (`"Viewer information (F1)"`); the script parses the int,
   walks back that many siblings, takes the child, clicks. Adding a tab is then a named-list edit.

**Two failure modes already paid for in Halo:**

- `IsElementVisible` on a control inside the tab returns `true` even when the tab is inactive —
  it only tests presence in the tree. Any "open it if it isn't open" guard built on it never
  fires. Click unconditionally, or use `IsVisiblyRendered`.
- The whole tray can be minimized independently of tab selection. When it is, *none* of the tab
  toggles resolve. Check for the maximize control first.

---

## `DictateSection` — initialize Report Builder for the open case

Reads the accession off the open case (label element, value in the next sibling — or parse the
window title if the vendor does not expose it in the DOM), sets `CaseNumber`, then branches:

- **Premium**: `DocumentStore.LoadAndLock` → `_Premium.GetWritelock` → `_Premium.SetupDocument` →
  `SpeechBox.StartDocument` → focus the Diagnosis section.
- **Classic**: recover or create a `TextEditor` document, or focus the matching field in the IMS
  report directly and `Ctrl+Home`.

The case type is derived from the accession (usually the first two characters) and passed through
the case type named list to pick the Report Builder template.

**Confirm:** the accession label and its relationship to the value; the case-type substring rule;
the spoken-section → field-label mapping for Classic.

---

## `ReturnTo<System>` — send Report Builder text back to the IMS

Iterate the document's sections, match each to an IMS field by the part's `Label`, and
`SetElementText` into it. Then `MarkEvent`, advance the stage (`Final`, or `Amended` if already
final), `SaveAndRelease`, close, and restore the command document.

Matching on `part.Label` rather than a hardcoded list means adding a section to the template needs
no script change — as long as the IMS field's accessible name matches the label exactly.

Skip sections shorter than a few characters; empty sections would otherwise clear fields the
pathologist filled in by hand.

**On failure, warn — do not error and do not release the document.** The dictation is still in
Report Builder and the pathologist can retry. Releasing a document whose text never landed loses
the dictation.

Palette entries go under both `SpeechBox Editor` and `Text Editor`.

---

## `PullBiomarkerResults` and the Labels namespace

See `ai-resulting.md`.
