# Script catalog

What each script is for, which variants exist in the shipped integrations, and what has to be
confirmed against the live application before it works.

---

## `_CaseNumber` — accession formatter

`ExtensionScript, ISpeechFormatter`. Assembles the accession number from the spoken case type,
year and digits. Every namespace carries its own because the format differs per system.

**Confirm:** the exact assembled shape against the customer's real accession numbers. Do not
carry a shape over from another integration — the separator placement differs per system, and
`Fuji` does not even follow the `_CaseNumber` naming convention (its formatter is
`FormatFujiCaseNumber`).

`Property()` does not work here — see the `ISpeechFormatter` section in `conventions.md` before
you wire any of the declared properties up.

### A hardcoded year is normal *during* development — strip it before distribution

The published `Halo._CaseNumber` contains `string caseNumber = caseType + "-23-" + fullNum;`. That
is **deliberate**: the sandbox's test cases are all year 23, and hardcoding it means not having to
speak the year on every `CaseNumber` command while developing. It is a convenience, not a defect.

It does have to come out before the integration goes anywhere near a customer. The correctly
derived year is already computed a few lines above (`DateTime.Now.Year...Substring(2)`) and simply
overwritten, so the fix is to use the variable that is already there.

Two things follow from this:

- **Don't copy a hardcoded year forward.** If you scaffold from an existing `_CaseNumber`, that
  literal is the first thing to check.
- **If you hardcode one yourself, say so in a comment** with what it should become, so the next
  reader can tell a sandbox shortcut from a mistake.

**Genuine things to get right:**

- **The zero pad is guarded by the palette, not the code.**
  `new string('0', counterLength - shortNum.Length)` throws `ArgumentOutOfRangeException` if more
  digits arrive than `CounterLength`. In Halo that cannot happen, because the trigger caps the
  repeat at `[<Digit>|1-5]` against a `CounterLength` of 6 — the constraint lives in the command
  palette.

  Know where the guard is. If you widen the `<Digit>` repeat on the trigger, or lower
  `CounterLength`, this line starts throwing and nothing in the script will tell you why.
  `shortNum.PadLeft(counterLength, '0')` removes the coupling and degrades instead of throwing, so
  prefer it in new code — but the existing form is not a live defect.
- **Read the property name back exactly as declared.** `Property("LeadingZeroes", ...)` against a
  declared `LeadingZeros` silently returns the default forever.
- Note that a declared property an `ISpeechFormatter` never reads is inert — `Halo`'s
  `LeadingZeros` is accepted by the static overload and ignored, and its declared `CounterLength`
  default of 5 sits next to a hardcoded 6. Harmless, but don't expect the palette to control it.

---

## `_<System>` — extension library

Window and title discovery, plus anything more than one command needs. Two title helpers, and
picking the wrong one is a recurring bug — see `browser-api.md`.

Systems reached by URL rather than by clicking the worklist also put their deep-link builders and
an `EnsureWindow` helper here: one reused window per surface (e.g. Pathologist Review, Slide
Viewer), each pinned to a configured monitor.

> The reference implementation for this was `_Flexlis.cs`. **There is no `FlexLIS` namespace in
> the current instance** — verify it exists in the tenant you are reading before citing it, and
> build the pattern from `_Window` in `browser-api.md` if it does not. The pattern is sound; the
> example may not be available to you.

---

## `CaseNumber` — open a case from the worklist

Two shapes:

1. **Worklist search** (Halo, AISight, the generated template). Set the filter column, type the
   accession, wait for the row, click the link. Works everywhere; depends on element names.
2. **Deep link.** Build the URL for each surface and navigate a reused window to it.
   Much more robust when the vendor has stable routes — no dependence on column order or on the
   viewer link being at a fixed child index. Prefer this when the vendor exposes routes.

Both set `CaseNumber` as a state property *first*, then optionally start Report Builder.

**Confirm:** the filter control and its accession option, the search field's name, whether the
result link is a `Hyperlink` or a `Button`, and whether the click opens the case in the same
window or a new one.

---

## `NavigateCases` / `ReturnToWorklist` / `NextCase` — three different commands

These get conflated constantly, and the shipped scripts are themselves inconsistent. Pick the
name that matches the *behavior*:

| Command | Surface | What it does |
|---|---|---|
| `NavigateCases` | IMS | Presses the next/previous case control **in the slide viewer** to move through the worklist. Bidirectional, driven by `<NextPrevious>`. |
| `ReturnToWorklist` | IMS | Sends the user out of the open case, back to the worklist. |
| `NextCase` | **LIS only** | Closes out the current case and resets the LIS to a blank state, ready for `CaseNumber` to search for the next one. |

**Do not scaffold `NextCase` for an IMS.** It is an LIS concept. The legacy CoPath and PowerPath
help text is the definition: *"Save the case, then either Close or Clear the case from Case
Information Window."*

The shipped IMS scripts do not respect this, so **do not infer behavior from the name when
reading the cache.** Every one of these is named `NextCase` today:

- `Fusion` — `<NextPrevious>` → clicks `"Next case"` / `"Previous case"`. This is `NavigateCases`.
- `AISight` — clicks `"Go to next accession"`. `NavigateCases`, one-directional only.
- `Concentriq` — clicks the `format_list_bulleted` link. This is `ReturnToWorklist`.
- `Corista` — clicks the `"Corista"` logo hyperlink. `ReturnToWorklist`.
- `PathPresenter` — clicks the return arrow beside the logo. `ReturnToWorklist`; its own comment
  says so.
- `Fuji` — clicks `"All Cases"` → `"My Cases"` → `"All Cases"`. A worklist *refresh*, not case
  navigation at all.

`AISight` additionally has a correctly-named `ReturnToWorklist` alongside its `NextCase`, so that
namespace carries both actions under mismatched names.

### Writing `NavigateCases`

Runs against an open case, so it needs `FindCurrentTitlePageByRegex`, not
`FindCurrentTitlePage` — and note that several of the scripts above get this wrong too.

Two proven shapes:

- **Caption-Text idiom** (Halo): find `"Next Case"` / `"Previous Case"` as `Text`, click the
  parent. Searching for a `Button` by that name returns null.
- **Real named buttons** (Fusion): `FindElementOnPage(manager, "Next case", UIAControlType.Button)`
  works directly. Note the casing differs from Halo's — `"Next case"` vs `"Next Case"`.

Not every viewer has these controls. If this one does not, say so and drop the script rather than
shipping something that half-works — and check whether what the vendor actually offers is a
return-to-worklist instead.

### Writing `ReturnToWorklist`

One click, but the control is almost never named usefully. The three shipped versions each anchor
differently: a Material ligature (`format_list_bulleted`), the product logo as a `Hyperlink`, and
a sibling walk from the logo `Image`. Read `browser-api.md` on ligatures and anchor walks first,
and do **not** copy PathPresenter's or BXLink's anchor — both address Chrome's missing-image
placeholder string, which is documented there as a hard no.

---

## `NavigateSlides` — next/previous slide

A keystroke, and usually nothing else. `PageDown`/`PageUp` in Halo and AISight. Some viewers use
`Down`/`Up` and need the image library panel opened first — confirm both the key and the
precondition against the live viewer.

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

The palette entry goes under `SpeechBox Editor` for the Premium path, and additionally under
`Text Editor` only if the site runs Classic. The trigger is `send report` — the same phrase on
every LIS and IMS, deliberately. Do not add a `return to <System>` alternative.

**The script keeps the `ReturnTo<System>` name.** That is the long-standing convention and it
stays — `ReturnToHaloAP`, `ReturnToAISight`, `ReturnToFusion`, `ReturnToPathFlow`. Name it for the
product, not the namespace, where those differ. Only the *spoken phrase* was standardized to
`send report`; the script name was never part of that change.

---

## `PullBiomarkerResults` and the Labels namespace

See `ai-resulting.md`.
