# VOScript house conventions

## File and class naming

Script files are flat, dotted, one class each:

```
VOScript.Starter.Halo._CaseNumber.cs
VOScript.Starter.Halo._Halo.cs
VOScript.Starter.Halo.CaseNumber.cs
VOScript.Starter.Halo.Labels._HaloBreastBmk169Labels.cs
```

The class name must equal the last dotted segment before `.cs`. An underscore prefix marks a
library (`ExtensionScript`) rather than a voice command (`CommandScript`).

### `VOScript.Starter` vs `VOScript.Standard`

A PRO instance carries both. **`VOScript.Standard` is the old starter code**, still live for
existing customers. **`VOScript.Starter` is the current line**, restructured so new work does not
disturb shipped configs. All new work goes in `VOScript.Starter`; treat `VOScript.Standard` as
read-only unless told otherwise.

A few systems exist in both — `PathFlow`, `PowerPath`, `DefaultCaseNumber` — so confirm which line
is meant before editing anything whose name appears twice.

`VOScript.Standard.SpeechBox.*` and `VOScript.ReportBuilder.*` are product-side scripts that
palettes reference directly for editor commands. They are not part of a system integration and are
not an editable surface.

### Sub-namespaces

Shared libraries sit directly under `VOScript.Starter`, not inside a system:
`VOScript.Starter._Common`, `._Browser`, `._Desktop`, `._Premium`, `._Scanning`, and
`VOScript.Starter.Browser.Manager.BrowserManager`. AI resulting overlays live one level down, in
`VOScript.Starter.<System>.Labels`.

Beyond that, **an IMS namespace stays flat** — `VOScript.Starter.Halo.ShowTab`. Large LIS
integrations do not: Epic and PowerPath group into a functional tier (`.Core`, `.Ancillary`,
`.Recorder`, `.Scanning` / `.Scanner`, `.Transcription`), giving keys like
`VOScript.Starter.PowerPath.Core.SignoutReport`. See the `voscript-lis` skill.

Exported PRO configs arrive as `R_ScriptSource/<ExportKey>.xml` with the source in the
`ScriptCode` attribute, XML-escaped, and declared properties mirrored in `<ScriptProperties>`.
Revision suffixes on hand-saved copies (`.rev5P.txt`) mean revision 5, Published; `A` is Active.

## Skeleton

Every script — library or command — has this exact shape. The regions are parsed by
VO_ScriptEdit; do not reformat or drop them.

```csharp
#region USING NAMESPACES
using System;
...
#endregion USING NAMESPACES

namespace VOScript.Starter.<System>
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    // attributes go here, or nothing at all
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class <ScriptName> : CommandScript
    {
        public override void Execute()
        {
            // Author: <email>
            // Date: <M/D/YYYY>
            // Use:
            // This will ...
            //
            // *****
        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
```

`ExtensionScript` subclasses expose `public static` helpers and have no `Execute`.

## Palette properties

Declared as attributes in the PROPERTYDEF region, read back with `Property("Name", default)`.

```csharp
[ExtensionCommandClass(HelpText = "Will enter the spoken accession number and open the case.")]
[ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length of the counter part.")]
[ExtensionStringProperty("CaseTypeList", DefaultValue = "CaseType", HelpText = "Named list of case types.")]
[ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Pad the counter with zeros.")]
```

Use `[ExtensionExtensionClass(...)]` on an `ExtensionScript`, `[ExtensionCommandClass(...)]` on a
`CommandScript`.

**Read the property name back exactly as declared.** `Property("LeadingZeroes", true)` against a
declared `LeadingZeros` silently returns the default forever — this bug shipped in more than one
namespace before it was caught.

### A declared property an `ISpeechFormatter` never reads is inert

`_CaseNumber` legitimately keeps a `CaseTypeList` property, for the reason given under named lists
below. But an `ISpeechFormatter` is entered through
`FormatParameterizedSpeech(SpeechParameters args)` — there is no trigger and no palette position to
read from — so it is easy to declare a property and then quietly hardcode the value instead. The
shipped `Halo._CaseNumber` does exactly that:

```csharp
public string FormatParameterizedSpeech(SpeechParameters args)
{
    int counterLength = 6;                  // the attribute declares DefaultValue = 5
    string caseTypeList = "HaloCaseType";   // the attribute declares "CaseType"
    bool leadingZeros = true;               // declared, accepted by the overload, never used
    return FormatParameterizedSpeech(args, counterLength, caseTypeList, leadingZeros);
}
```

Nothing breaks, but the palette shows three values the script does not use. If a value genuinely
needs to be site-configurable, the calling `CommandScript` has to read it and pass it into the
static overload. Otherwise keep the declared default and the hardcoded value in agreement, or drop
the attribute.

## Speech parameters and named lists

```csharp
string direction   = SpeechParams.TranslateSingle("NextPrevious");
string firstParam  = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
string tool        = SpeechParams.TranslateToParams("HaloSlideMarkup").Translation;
string template    = SpeechParams.TranslateToParams("HaloCaseType").Target;
TranslationParams p = Application.FindTranslationParams("HaloCaseType", "SP");
```

`.Translation` is what the list maps the spoken word to. `.Target` is the secondary column,
used to carry the Report Builder template for a case type.

### Read the list with `SpeechParams.Names[0]`

```csharp
string spoken = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
```

That is the default and it covers nearly every command. The trigger in the palette already names
the list, so `Names[0]` follows whatever list the trigger uses. The script never needs to know the
list's name.

**Do not put the list name in a palette property.** A `[ExtensionStringProperty("MagnificationList",
DefaultValue = "HaloMagnification")]` read back as `Property("MagnificationList", ...)` adds a layer
that can only go wrong: a typo in the property name silently returns the default forever, and the
value duplicates something the trigger already states. It shipped that way in one template and had
to be unwound.

**More than one spoken parameter? Name the lists.** Not `Names[1]`, `Names[2]` — name them:

```csharp
string markupType = SpeechParams.TranslateSingle("HaloSlideMarkup");
string layer      = SpeechParams.TranslateSingle("HaloMarkupLayers");
```

Positional indexing looks tidier and is a trap. A second parameter is usually *optional* — a
`SlideMarkup` where the layer may or may not be spoken — and when it is omitted, everything after
it shifts down an index. The tool would be `Names[0]` on one utterance and `Names[1]` on the next.
Naming is stable regardless of what was spoken.

This is safe because list names are namespaced per system, so they do not collide and do not move.

**Name the lists `{System}` + purpose.** Halo is the reference:

| List | Read by |
|---|---|
| `HaloCaseType` | `_CaseNumber`, `CaseNumber` |
| `HaloMagnifications` | `SetMagnificationLevel` |
| `HaloSlideMarkup` | `SlideMarkup` |
| `HaloMarkupLayers` | `SlideMarkup`, where the system has layers |
| `HaloRotation` | `RotateSlide` |
| `HaloTabs` | `ShowTab` |

Some older namespaces differ — `ConcentriqMagnification` is singular, `ConcentriqMarkupButtons`
instead of `SlideMarkup`. Do not copy those; use the Halo names for anything new.

**`_CaseNumber` is a special case for a different reason.** Its parameters are optional and
repeating — the trigger is `<CaseType> [<Year> | ] [<Digit>|1-5]`, where `Year` may not be spoken
and `Digit` repeats one to five times — so it cannot index at all. It loops `args.Names` and tests
each against the list name:

```csharp
if      (args.Names[index] == caseTypeList) caseType = args.Translate(caseTypeList, args.Values[index]);
else if (args.Names[index] == "Year")       year     = args.Translate("Year", args.Values[index]);
else if (args.Names[index] == "Digit")      shortNumBuilder.Append(args.Translate("Digit", args.Values[index]));
```

That is why `_CaseNumber` — and only `_CaseNumber` — keeps a `CaseTypeList` property: it needs the
name to do the matching, and as an `ISpeechFormatter` it has no trigger to read a position from.

So: `Names[0]` when there is one spoken parameter, explicit list names when there are several.

Named lists are per-system. `Digit`, `Year` and `NextPrevious` are shared.

### The only hard requirement is that the trigger and the list agree

Use the Halo names above for anything new. But understand what actually enforces them: **nothing**
except the palette. The list name in the trigger has to match the name of the list, and no other
part of PRO reads it. That is why the older namespaces drifted, and why the drift is harmless
where it sits.

**The Named List Manager caps the name length,** which is the honest reason some suffixes are
shorter rather than anyone being careless. `PathPresenterMagnifications` is 27 characters and
became `PathPresenterMagLevels` at 22. When a Halo name will not fit, shorten the *suffix* -
`Magnifications` → `MagLevels`, `SlideMarkup` → `Markup`, `Rotation` → `Rotate` - and change the
trigger to match. Record which you used in the command's help text.

**Never assume the convention for a system that already exists.** Read its real list name off its
command palette entry, or out of `GlobalSetup`. The shipped spread, beyond the Concentriq cases
noted above:

| Concept | Names in use |
|---|---|
| Magnification | `…Magnifications` (AISight, BXLink, Fusion, Halo, MacroPath, VBPathView), `…Magnification` (Concentriq, Corista), `…MagLevels` (PathFlow, PathPresenter) |
| Markup | `…MarkupButtons` (6 systems), `HaloSlideMarkup`, `PathPresenterMarkup`, `VBPathViewMarkup` |
| Rotation | `…Rotation` (Concentriq, Halo), `…Rotate` (AISight, PathPresenter, VBPathView) |
| Tabs | `…Tabs` (Fusion, Halo, PowerPath), `BXLinkTab`, `PathFlowSideMenu`, `AISightPanels` |

Only `<System>CaseType` holds everywhere.

### `.Target` needs an Extended list

`TranslateToParams(...).Target` returns a value only on a list whose type is `Extended` or `Full`.
`HaloCaseType`, `AISightCaseType`, `CoristaCaseType`, `EpicCaseType`, `PathFlowCaseType`,
`MockEpicCaseType` and `VBPathViewCaseType` are Extended and carry the Report Builder template in
the second column. `ConcentriqCaseType`, `FusionCaseType`, `BXLinkCaseType`, `MacroPathCaseType`
and `PathPresenterCaseType` are **Simple** — no second column, so a `.Target` lookup against those
returns nothing and the template has to come from elsewhere.

### Translations are compared case-sensitively

The shipped `Fusion.NextCase` branches on `direction == "NEXT"` — upper case — against the shared
`NextPrevious` list. Read the list's actual Text values before writing the comparison rather than
assuming `"Next"`, and prefer a case-insensitive compare in anything new.

### A translation holds ONLY the part of the phrase that varies

This is the convention that catches people out. The invariant words live in the trigger, and the
named list carries just the variable. For `SetMagnificationLevel` the trigger is

```
zoom <ConcentriqMagnification>
```

and the list's translations are `0.1x`, `20x`, `40x`, `In`, `Out`, `Reset` — **not** `Zoom to
0.1x`. So `TranslateSingle` hands the script `"0.1x"`, and anything else the target control needs
is composed in the script:

```csharp
string spoken = SpeechParams.TranslateSingle(Property("MagnificationList", "ConcentriqMagnification"));

switch (spoken)
{
    case "In":    menuItem = "Zoom In";      break;
    case "Out":   menuItem = "Zoom Out";     break;
    case "Reset": menuItem = "Reset Zoom";   break;
    default:      menuItem = "Zoom to " + spoken; break;
}
```

Do not push the invariant text into the list to save a line of code. The list is edited by people
reading it as a list of magnifications, and repeating `Zoom to` on every row makes it worse — and
a vendor that renames its menu caption then means editing every row instead of one line.

Where a translation happens to equal the control's whole on-screen name — the annotation tools in
`SlideMarkup` are `Rectangle`, `Ruler`, `Pin` in both the list and the menu — no composition is
needed. That is a coincidence of that menu, not a different rule.

## State properties

Cross-script scratch space on the application:

```csharp
Application.SetStateProperty("CaseNumber", caseNumber);
string edition = StateProperty("Edition", "");     // "Premium" | "Classic"
string openRB  = StateProperty("OpenRB", "No");    // "Yes" | "No"
string role    = StateProperty("RoleCategory", "");// "GROSS" | "RESIDENT" | "SIGNOUT"
```

`Edition`, `OpenRB` and `RoleCategory` are **defined** properties with fixed choice lists. Match
them exactly:

- `Edition` — `Classic` | `Premium`
- `OpenRB` — `Yes` | `No`, set per User and per Role. **Not** `True`/`False`; a default of
  `"False"` never equals a real value, so the comparison takes the wrong branch forever.
- `RoleCategory` — `GROSS` | `RESIDENT` | `SIGNOUT`

`CaseNumber` is **not** a defined property. It is created at runtime by whichever script calls
`SetStateProperty` first, which is why it does not appear in the definitions. That works, but
nothing validates the key — a typo reads back empty rather than failing. It is the contract
between `CaseNumber` / `DictateSection` and everything downstream, so set it before anything slow
happens and spell it exactly.

## Command palette entries

A script does nothing until a palette item points at it. Exported shape:

```xml
<RC_Application ApplicationKey="Custom">
  <Items>
    <RC_ExtensionItem ItemKey="CaseNumber" ContentSource="Local">
      <Content ExtensionType="Command" Scope="Global" ExtensionClass="VOScript.Starter.Halo.CaseNumber">
        <Properties>
          <RC_ExtensionProperty PropertyKey="CounterLength" Value="5" />
        </Properties>
        <Triggers>
          <RC_CommandTrigger TriggerDeviceKey="Speech"
            TriggerCode="&lt;HaloCaseType&gt; [&lt;Year&gt; | ] [&lt;Digit&gt;|1-5]" />
        </Triggers>
      </Content>
    </RC_ExtensionItem>
  </Items>
</RC_Application>
```

`ApplicationKey` decides where the command is live: `Custom` (the IMS window), `Always On`,
`SpeechBox Editor` (Report Builder, Premium), `Text Editor` (Classic).

Standard triggers. These are the live forms read off the `Indica Halo` palette — the most complete
shipped IMS set — with the list name generalised. Check the target system's own palette before
reusing one.

| Command | Application | Trigger |
|---|---|---|
| `CaseNumber` | Custom | `<SystemCaseType> [<Year> dash\|] [<Digit>\|1-5]` |
| `NavigateCases` | Custom | `<NextPrevious> case` |
| `NavigateSlides` | Custom | `<NextPrevious> slide` |
| `RotateSlide` | Custom | `rotate to <SystemRotation>` |
| `SetMagnificationLevel` | Custom | `zoom <SystemMagnifications>` |
| `ShowTab` | Custom | `show <SystemTabs>` |
| `SlideMarkup` | Custom | `add <SystemSlideMarkup>` |
| `DictateSection` | Custom | `dictate <$Sections>` |
| `ReturnTo<System>` | SpeechBox Editor | `send report` |
| `PullBiomarkerResults` | SpeechBox Editor | `get results` |

Four of these are easy to get wrong:

- **`<Year> dash`, not `<Year> |`.** The year is only accepted when the pathologist also says
  "dash", and `_CaseNumber` relies on that to know a year was spoken at all.
- **`show <SystemTabs>` takes no `$`.** The `$` prefix marks a *dynamic* list computed from the
  active document — `<$Sections>`, `<$ListItems>`, `<$PlainText>`. Tab lists are ordinary static
  named lists, so a `$` on one makes the trigger resolve to nothing.
- **`RotateSlide` and `SlideMarkup` carry a verb** (`rotate to`, `add`). A bare
  `<SystemSlideMarkup>` with no invariant word is not the shipped shape.
- **`send report` is the phrase on every LIS and IMS.** Deliberately identical everywhere, so a
  pathologist working across two systems says the same thing — do not add a
  `return to <System>` alternative. This is about the *phrase* only; the script keeps its
  long-standing `ReturnTo<System>` name, named for the product where that differs from the
  namespace (`ReturnToHaloAP`).

`PullBiomarkerResults` is the class name; its palette item is commonly keyed `GetResults` with the
trigger `get results`. Palette key, trigger and class name are three separate things and none has
to match the others.

## Report Builder handoff

Premium (SpeechBox) path, used by both `CaseNumber` and `DictateSection`:

```csharp
IDocument caseDocument = DocumentStore.LoadAndLock("Document");

if (_Premium.GetWritelock(caseDocument, Application))
{
    _Premium.SetupDocument(caseDocument, DocumentStore, "Document", template, "SIGNOUT");
    SpeechBox.StartDocument(caseDocument);

    IDocPart diagnosis = caseDocument.FindActivePart("Diagnosis", DocPartTypes.Section);
    if (diagnosis.Visible == false)
    {
        diagnosis.Visible = true;
        SpeechBox.RefreshDocumentChanges();
    }
    SpeechBox.SetInputFocus(diagnosis);
}

SpeechBox.SpeechInputAnchor.Enable();
```

Check the cheap `OpenRB` flag *before* taking the write lock — `GetWritelock` has side effects
and should not run on a path that is about to be abandoned.

Classic (TextEditor) path is the `else` branch: `TextDocumentStore.Load(documentID)`,
`TextEditor.StartDocument`, `TextEditor.SpeechInputAnchor.Enable()`.

Writing back out:

```csharp
SpeechBox.ActiveDocument.MarkEvent("Report successfully transferred to AP System");
if (SpeechBox.ActiveDocument.Stage == "Final") SpeechBox.ActiveDocument.MarkNewStage("Amended");
else if (roleCategory == "SIGNOUT")            SpeechBox.ActiveDocument.MarkNewStage("Final");
if (DocumentStore.SaveAndRelease(SpeechBox.ActiveDocument)) SpeechBox.Close();
Application.SetCommandDocument("Doc-<System>-Surgical");
```

Once the document is open, reading and changing its parts is covered in `report-builder-parts.md`.
That doc covers lists, adding specimens from templates, PartKey matching, detecting an undictated
placeholder, picklist fields, the current specimen and the cursor.

## Not available in PRO

- **`System.Speech` / `SpeechSynthesizer`.** PRO does not reference it, so a script cannot speak
  text back to the pathologist. This was tried and abandoned — do not reach for it again. To
  surface information to the user, write to `StatusLog` (the toolbar shows entries up to the level
  set by the `ToolbarMessageLevel` state property), or set a state property and let the template
  render it.

## Error handling

```csharp
catch (Exception ex)
{
    StatusLog.WriteErrorEntry($"Error controlling {Vendor}: {ex.Message}", ex);
}
finally
{
    controller?.Dispose();   // omit only when the controller is in a using
}
```

Throw `ClientException` for something the user or the palette config must fix — it surfaces to
the user. Use `StatusLog.WriteWarningEntry` for a degraded-but-continuing condition.
