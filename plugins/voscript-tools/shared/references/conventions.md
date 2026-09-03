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

## Speech parameters and named lists

```csharp
string direction   = SpeechParams.TranslateSingle("NextPrevious");
string firstParam  = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
string tool        = SpeechParams.TranslateToParams("HaloAnnotationTools").Translation;
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

Named lists are per-system. The house names are `<System>CaseType`, `<System>Magnification`,
`<System>MarkupButtons`, `<System>Rotation`. `Digit`, `Year` and `NextPrevious` are shared.

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
string spoken = SpeechParams.TranslateSingle(Property("MagnificationList", "ProsciaMagnification"));

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
string openRB  = StateProperty("OpenRB", "False"); // palette-controlled
string role    = StateProperty("RoleCategory", "");// e.g. "SIGNOUT"
```

`CaseNumber` is the contract between `CaseNumber` / `DictateSection` and everything downstream.
Set it before anything slow happens, so a later script never reads a stale value.

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

Standard triggers:

| Command | Application | Trigger |
|---|---|---|
| `CaseNumber` | Custom | `<SystemCaseType> [<Year> \| ] [<Digit>\|1-5]` |
| `NavigateCases` | Custom | `<NextPrevious> case` |
| `NavigateSlides` | Custom | `<NextPrevious> slide` |
| `RotateSlide` | Custom | `rotate [<Digit>\|1-3]` |
| `SetMagnificationLevel` | Custom | `zoom <SystemMagnification>` |
| `ShowTab` | Custom | `show <$SystemTabs>` |
| `SlideMarkup` | Custom | `<SystemMarkupButtons>` |
| `DictateSection` | Custom | `dictate <$Sections>` |
| `ReturnTo<System>` | SpeechBox Editor, Text Editor | `[send report\|return to <System>]` |
| `PullBiomarkerResults` | SpeechBox Editor | `pull biomarker results` |

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
