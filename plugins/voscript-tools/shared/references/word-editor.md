# Word as the report editor

For an LIS that edits its report in Microsoft Word (PowerPath, CoPath, Meditech, SoftPath). Covers reaching Word,
the shared `_Word` library, how a report's sections are laid out, and the traps that have already cost real time.

Everything here was worked out, and live-tested, on PowerPath (`demo\sales`, 2026-09-28). The shapes transfer to
other Word LISs; the layout does not - see "Section layout" before assuming it.

## Reach Word through NetOffice, not keystrokes

`MSWordTools.Instance.GetWordApplication(hWnd)` returns a **`NetOffice.WordApi.Application`** for the Word window you
hand it. From there it is the full object model - `ActiveDocument`, `Range(start, end)`, `Find`, `Paste`,
`UndoRecord`, `ScreenUpdating` - so nothing needs Word in the foreground and nothing is typed.

```csharp
using Word = NetOffice.WordApi;
using WordEnums = NetOffice.WordApi.Enums;

Word.Application word = MSWordTools.Instance.GetWordApplication(hWnd);
Word.Document doc = word.ActiveDocument;
```

Do not build new work on the `MSWordTools.Selection*` wrappers plus `Automation.Send("^v")` / `"+{LEFT}"`. That is what
the old PowerPath ReturnToWord did: slow, focus-dependent, and able to stop halfway through a transfer.

## `VOScript.Starter._Word` - the generic part

A prerequisite (tier `word`). Only what every Word editor needs:

| Member | What it is for |
|---|---|
| `Edit(Application, hWnd, undoName, doc => ...)` | Runs an edit with screen updating off, as **one** undo step, and captures any Word failure. **Always go through it** - see the SerializationException trap below. |
| `EditableRanges(doc)` | The Word sections the user can type in under form protection. |
| `PasteDocPart(doc, start, end, part)` | Replaces a range with a Report Builder part, formatting included (`RenderToClipboard(RenderFlags.None)` then `Range.Paste()`). |
| `TopLevelSections(document)` / `HasDictation(part)` | Report Builder rules: skip nested sections (Synoptic inside Diagnosis), and ignore an untouched part that renders only `A.`. |
| `WaitUntilResponds(hWnd)` | A report window appears before Word is usable; wait for it to answer automation instead of a fixed second. |

How a report **lays out** its sections is deliberately *not* in `_Word`. It differs per LIS, so it lives in that LIS's
own library (`_PowerPath.TransferReportToWord`).

## Section layout differs per LIS - find out before writing the locator

| LIS | Layout | How a section's body is found |
|---|---|---|
| PowerPath | Headers are plain text (`FINAL DIAGNOSIS:`) in one editable Word section; demographics in a form-protected section above. | Find the label in the editable sections; the body runs to the next header, or to the section break. |
| CoPath | A **protected** Word section per header, alternating with an editable section per body. | The shipped `VOScript.Standard.CoPath.SpeechBox.ReturnToWord` jumps by position (Word section `2 × (i+1)`). Better: pair each editable section with the header text in the protected section before it, and match on the Label. |

The LIS scaffold generates the PowerPath layout as the default, with a TODO to confirm. For a protected-header layout,
replace `LocateHeaders` / `FillSections` in `_<System>` and keep going through `_Word`.

Dump a real report before choosing. Read-only, from PowerShell against the report window:
`doc.ProtectionType` (`2` = allow only form fields), and per section `Sections[i].ProtectedForForms` and `Range.Text`.

## Traps

- **`Find` over protected text throws** - *"This method or property is not available because the object refers to a
  protected area of the document."* PowerPath's report is form-protected, so a Find over `doc.Content` fails. Search
  each of `_Word.EditableRanges(doc)` instead.
- **A NetOffice exception must not escape the script.** It reaches PRO only as
  *"SerializationException: Type 'NetOffice.Exceptions.MethodCOMException' ... is not marked as serializable"*, which
  hides the cause entirely. `_Word.Edit` catches it and logs `ex.Message` plus `ex.InnerException.Message`.
- **Never paste over a section break.** The last section's body stops one character short of its Word section's end.
  Filling "to the end of the document" (what the first rewrite did) runs through the trailing section breaks.
- **Nested Report Builder sections have no header of their own.** `EnumerateParts(Section)` returns Synoptic as well as
  the Diagnosis it sits in; without `TopLevelSections` the transfer stops on a "missing" SYNOPTIC REPORT header.
- **Fill bottom-up.** Every paste moves everything after it; working from the last header to the first keeps the
  earlier offsets valid.
- **Check every header before writing anything.** A missing header on section 4 must not leave sections 1-3 already
  written - the old script did exactly that.

## Optional sections

A Report Builder section tagged `optional` (e.g. FROZEN SECTION) may have no header in the stock Word template. When
it has dictation, `TransferReportToWord` inserts its label as plain text ahead of the next section in outline order -
headers only have to be visible, not formatted. An untagged section with a missing header stops the transfer.

## Word and the LIS save step

With Word as the editor the chain is `ReturnToWord` (Report Builder into Word) then `ReturnTo<LIS>` (Word saved back
into the LIS, its prompts, the status step). `CaseComplete` runs both, then `NextCase`. The status step never signs a
case out - see `script-catalog.md`, `ReturnTo<LIS>`.
