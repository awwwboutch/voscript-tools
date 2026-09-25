# Working inside the Report Builder document

How a script reads and changes the parts of an open Report Builder (SpeechBox) document: lists,
the specimens in them, their fields, and the cursor. Opening the document in the first place is
the handoff in `conventions.md`; this is what comes after.

Everything here was learned building and testing `Lumea.LumeaCameraCapture` (see `examples/`),
which builds and fills gross specimens from a camera. It applies to any script that writes into
a list: building a diagnosis from the gross, seeding specimens from the LIS, filling measurements
from a device.

## Lists and their items

The lists are always `GrossList`, `DiagnosisList`, `MicroList` and `CommentList`. That naming is
the convention, so no property is needed to find them.

```csharp
IDocList grossList = SpeechBox.ActiveDocument.FindActivePart("GrossList", DocPartTypes.List) as IDocList;
```

`IDocList.Items` is enumerable and indexable, and `Items.Count` is a property. Each item is an
`IDocListItem` with an `Index` (0-based; convert to a part letter with `_Common.ConvertNumToLetter`
or `(char)('A' + index)`).

### Adding a specimen from a template

```csharp
IDocListItem part = SpeechBox.FindLibraryPart("GrossList." + templateName) as IDocListItem;
grossList.Items.Add(part);
SpeechBox.RefreshDocumentChanges();          // before touching the part's fields
part.FindField("Site").Value = "left apex";
```

- `FindLibraryPart` returns **a new copy** every call, so call it once per specimen.
- Refresh after `Items.Add` and before filling fields.
- `null` means the template does not exist in that library. Say which template and stop; do not
  carry on adding the rest.
- `grossList.Items.Clear()` is safe. Nothing else depends on the gross list's original items, and
  grossing is always finished before diagnosis starts.

### PartKey: the template name is the LAST segment, with a copy number

`IDocListItem.PartKey` (the export key) is outline-qualified:

```
OutlineName.Gross.GrossList.GrossPart
OutlineName.Gross.GrossList.ProstateBiopsy
OutlineName.Gross.GrossList.ProstateBiopsy2     <- second copy
...
OutlineName.Gross.GrossList.ProstateBiopsy6     <- sixth copy
```

Two traps, both of which have cost a test cycle:

1. **Everything before the last `.` is not stable.** A fresh `FindLibraryPart` copy does not carry
   the same prefix as a specimen already in the document. Never compare whole keys.
2. **Every copy after the first gets a number appended, with no separator.** An exact comparison
   matches only the first copy. So with six cores, a check on the cursor's specimen fails for
   parts B–F.

Match on the last segment with any trailing number stripped from both sides:

```csharp
private static string TemplateOf(IDocListItem item)
{
    string key = item?.PartKey ?? "";
    return key.Substring(key.LastIndexOf('.') + 1);
}

private static string StripCopyNumber(string name) => Regex.Replace(name ?? "", @"[\W_]*\d+$", "");

private static bool IsTemplate(IDocListItem item, string template) =>
    string.Equals(StripCopyNumber(TemplateOf(item)), StripCopyNumber(template), StringComparison.OrdinalIgnoreCase);
```

Stripping both sides keeps a template whose own name ends in a digit (`Biopsy1`) matching too.

This is **not** the CAP-template PartKey used by the AI-resulting pipeline. That one is the CAP
XML's hidden `filename` field; see `ai-resulting.md`.

## Is a specimen still undictated?

Sites implement with **one empty placeholder specimen** in the gross (commonly `GrossPart`), so the
grosser can start dictating straight away. A script that builds the gross has to tell "only the
untouched placeholder" apart from "the grosser has started".

`RenderText` on an untouched item is **not empty**:

- It includes the part label Report Builder adds by itself (`A.`), **when labels are shown**.
  Sites usually hide labels on one-part cases, so code must work with and without them.
- Purple prompt text (e.g. `Cassettes:`) does **not** render.

So strip whitespace and the leading label before looking for dictation:

```csharp
private static string Normalise(string text)
{
    string squashed = new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
    return Regex.Replace(squashed, @"^\(?[A-Za-z0-9]{1,2}[.)]", "");   // "A." / "1)" / "(B)"
}

bool untouched = IsTemplate(item, "GrossPart")
              && !Normalise(item.RenderText(RenderFlags.None)).Any(char.IsLetterOrDigit);
```

If a site's placeholder has fixed text of its own, compare the normalised text with a fresh
`FindLibraryPart` copy of the placeholder, normalised the same way.

**Never overwrite.** Clear the gross only when it is still just the untouched placeholder;
otherwise append. Before filling a field, check it is empty, and refuse with a dialog if not.

## The current specimen

"Current" is whichever list item holds the cursor:

```csharp
using VOScript.Standard.SpeechBox;   // ListTools

IDocListItem current = ListTools.FindTopListItem(SpeechBox.InputFocus) as IDocListItem;
```

Check the result is actually in the list you mean (`ReferenceEquals` against `grossList.Items[i]`).
Focus can be in another section.

## Fields

`FindField(exportKey)` on the item finds a field by its export key. It returns `null` when the
template has no such field, which is normal (a skin template has no weight). Fall back to
`SpeechBox.ActiveDocument.FindField` only for a value the template captures once per case.

### Text fields vs picklist fields

- **Text field:** set `.Value`.
- **Picklist field** (`IN=Picklist` / `MultiPick` / `OrderedPick` in template scripting): set it to
  the **answer's ExportKey**, or its AltData via `.ReferenceValue`. Free text such as the answer's
  lowercase display text does not select anything.

`FragmentCount` (Library-Shared → ValueTypeGross) is the common case. Its answers' ExportKeys are
`One`, `Two`, `Three`, `Four`, `Five`, `Six`, `Multiple`; the text and phrase are lowercase; the
AltData is `1`–`6`. So 3 pieces is `"Three"` (not `"three"` or `"3"`), and 7 or more is
`"Multiple"`. Reading it back for an LIS, use `.ReferenceValue` to get the integer.

### Size

- **`FindField("Size")` always finds the active size field.** Templates with a `SizeSelector`
  question (answers One / Two / Range / Aggregate) swap which size fields exist. A script does
  not need to pick the answer first. Some templates name size `FromSize` or `Dimensions`, so try
  those in turn.
- **The value carries its unit** (`"0.5 cm"`, `"0.8 x 0.6 cm"`). Templates do not print the unit,
  because grossers dictate it and saying a unit makes Report Builder auto-advance to the next field
  (`AA=Units`).
- **One decimal**, always. Every lab reports that way.

### Template scripting

The template-script language, which covers field attributes (`[Field A= B= IN= AA= E= V= R=]`),
arrangement, behaviours and `Question.Answer` dot notation, is documented in the Google Doc
"Template script cheat-sheet" (id `199gQUNEZWImXUd6jI4PNUB6hLoAlIzFuNfUImtN_488`). A site's
exported `R_Template` XML reads as template-script text through the Drive connector, and it is
the fastest way to learn a customer's template and field names without access to their
environment.

## The cursor

`SpeechBox.SetInputFocus` takes a part or a field:

```csharp
SpeechBox.SetInputFocus(part.FindField("Size"));
```

Leave the cursor where the grosser's next words should go: the field just filled, or the first
field that still needs dictating. **Set it after `ShowSpeechDialog` returns**, not before; the
dialog takes focus.

## Audit trail

Only document **events** and **stages**:

```csharp
SpeechBox.ActiveDocument.MarkEvent("...");
SpeechBox.ActiveDocument.MarkNewStage("Gross Complete");
```

Both show in Document Manager → the document's History tab, with user, workstation and time.
There is nothing finer-grained.

## Filling the gross from a device or an external source

`Lumea.LumeaCameraCapture` is the worked example: a local camera (`_LumeaCamera`) returns
measurements, and the command turns them into specimens. What generalises:

- **Keep the transport in an `ExtensionScript` library and the document work in the command.** The
  library knows only HTTP and JSON; the command knows SpeechBox.
- **An external measurement rarely identifies the specimen.** Only a fixed physical layout does,
  like the BxChip's six lanes for prostate. Otherwise let the grosser choose the template and fill
  the specimen they are on.
- **Decide what you will do before calling the device**, so a capture that would be refused is
  never taken. Change nothing until the result has been validated.
- **Keep parts positional.** When specimens are matched to the LIS by order, an empty result
  still gets its part, or everything after it shifts onto the wrong part.
- **Log the raw response** (minus any base64 images) on every call, so the first real run shows
  what the device actually sends.
- **Give it a simulate switch** that feeds canned responses through the same code path. Make it
  `static readonly bool`, not `const`: with a `const`, the compiler flags the other branch as
  unreachable (CS0162).
