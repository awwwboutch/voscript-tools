# Reference examples

Real published scripts, extracted 2026-09-01 from the `demo\sales` tenant. They live here so the
skill does not depend on what any given machine happens to have cached — **not every tenant
carries every integration**, and a QA or customer tenant may carry almost none.

Read these instead of assuming the local cache has a sibling to learn from. If the cache does hold
the integration you want, prefer it — it will be newer — but ask which tenant first.

Do not edit these. They are a snapshot for reading, not the source of truth. The source of truth
is whatever is published in PRO.

## What each one demonstrates

| File | The shape it shows |
|---|---|
| `Halo._Halo.cs` | Extension library: fixed-fragment and regex title lookup, and why an open case needs the regex form. |
| `AISight._CaseNumber.cs` | The `ISpeechFormatter` accession builder. |
| `Halo.CaseNumber.cs` | Worklist search where the filter column must be set before typing. Case row as a Hyperlink. |
| `Halo.NavigateCases.cs` | Caption-Text idiom — find `"Next Case"` as `Text`, click the parent. |
| `Concentriq.NavigateSlides.cs` | Material ligature as the element's own Name: `"chevron_right"` as a `Hyperlink`. |
| `Halo.ShowTab.cs` | Icon tray with no usable names — the named list carries a sibling **offset** from a fixed anchor. |
| `Fusion.ShowTab.cs` | Mixed tray — decide `TabItem` vs `Button` per name, then focus or click. |
| `BXLink.ShowTab.cs` | Tab absent because its **panel** is collapsed. Open the panel, then check `IsTabSelected`. |
| `Halo.RotateSlide.cs` | No rotation input — read the current angle, compute a delta, press incremental hotkeys. |
| `Concentriq.RotateSlide.cs` | Rotation input as a `Spinner` named `"°"` that accepts `SetElementText`. |
| `Halo.SetMagnificationLevel.cs` | Caption-Text plus parent, off the viewer toolbar. |
| `Concentriq.SetMagnificationLevel.cs` | Hybrid — hotkeys where they exist, menu for the levels that have none. Also `"Zoom to " + value` composition. |
| `Halo.SlideMarkup.cs` | Annotation layer selected before the tool. Anchored off a hotkey-named button. |
| `AISight.DictateSection.cs` | Report Builder handoff, both Premium and Classic branches. |
| `AISight.ReturnToAISight.cs` | The return path — enumerate parts, write fields, `MarkEvent`, `MarkNewStage`, `SaveAndRelease`. |

## Reading these critically

They are production code, not exemplars. Two things in them are known to be worth *not* copying:

- Fixed `Thread.Sleep` calls where `_Browser.WaitForElement` would be correct.
- `Concentriq.SetMagnificationLevel.cs` clicks the menu item only inside its
  `if (!IsVisiblyRendered(...))` branch, so an already-open Zoom menu makes the command do nothing.

What they are reliably good for is the *shape* — which control types a vendor exposes, how a panel
is reached, what the Report Builder sequence is. For a system not represented here, the shapes
still transfer; the element names never do.
