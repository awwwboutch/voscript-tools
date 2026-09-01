# Browser automation surface

## Opening a page

Every browser-driving script starts the same way:

```csharp
string titlePage = _<System>.FindCurrentTitlePage(Application);
using var manager = new Browser.Manager.BrowserManager(titlePage);
using var controller = manager.GetBrowserController();
IUIAutomationElement document = controller.FindWebPageDocument(titlePage);
```

`BrowserManager` is `VOScript.Starter.Browser.Manager.BrowserManager`. When `using var` is not
used, dispose the controller in a `finally`. Not disposing leaks and eventually crashes PRO.

### Title discovery

Two library helpers, and which one you need depends on the window:

```csharp
// Fixed title fragment - the worklist, the app shell
_<System>.FindCurrentTitlePage(Application);

// Title that carries the case number - an open case
_<System>.FindCurrentTitlePageByRegex(Application, @"^[A-Z]{1,3}-?[0-9]{2}-[0-9]{1,6}");
```

Using the fixed-title version on a script that runs against an *open case* is a recurring bug:
the window is titled with the accession number by then, `FindCurrentTitlePage` returns empty,
and the script fails with a confusing "element not found". If a script acts on an open case,
use the regex form.

Case-number title patterns vary per site. `^[A-Z]{1,3}-?[0-9]{2}-[0-9]{1,6}` covers both
`S26-12345` and `XY-26-123456`. Confirm against the customer's real accession formats.

## `_Browser` helpers

Finding:

```csharp
FindElementOnPage(manager, name, UIAControlType.Button)
FindElementByAutomationId(automationId, controlType)          // page-wide
FindElementByAutomationId(root, automationId, controlType)    // scoped - prefer this
FindByPath(root, "outerId", "innerId", "leafId")              // narrows scope at each hop
GetElementMatchingRegex(...)  GetSiblingMatchingRegex(...)
```

Tree walking:

```csharp
GetParent(e)  GetChild(e)  GetChildByName(parent, name)       // GetChildByName is direct children only
GetNextSiblingElement(e, n = 1)  GetPreviousSiblingElement(e, n = 1)
GetLastSiblingElement(e)  GetLastSiblingOfType(e, type)
GetNthChild(...)  GetNthChildByType(...)  GetNthElementByControlType(root, type, n)  // 1-based
GetNthElementByAutomationIdAndControlType(...)                                       // 0-based
```

Presence and visibility:

```csharp
IsElementPresent(manager, name)      // exists in the tree
IsElementVisible(manager, name)      // back-compat alias of IsElementPresent - misleading name
IsVisiblyRendered(element)           // on screen, enabled, real geometry
```

Waiting — prefer these over `Thread.Sleep`:

```csharp
WaitForElement(manager, name, timeoutMs)
WaitForElement(manager, name, controlType, timeoutMs)
WaitForElementByAutomationId(manager, automationId, controlType, timeoutMs)
```

Acting:

```csharp
Invoke(element)   // InvokePattern -> LegacyIAccessible -> SelectionItem -> physical click
ClickElementOnPage(manager, name, controlType)
ClickElementByAutomationId(...)  ClickElementByAutomationIdAndControlType(...)
ClickPickerItem(...)             // opens a picker, waits for the item, clicks it
PressElementOnPage(...)  PressUIElementByName(manager, name)
ClickElement(e)  DoubleClickElement(e)  ControlClick(e)  GetClickPoint(e)
```

**The shipped integrations use `controller.Click(element)`.** `_Browser.Invoke` is the more
defensive helper — it tries InvokePattern, LegacyIAccessible and SelectionItemPattern before
falling back to a physical click — but these vendor UIs are Chromium trees where many "buttons"
are divs with an onclick handler and no InvokePattern, and a pattern-based invoke on those
silently does nothing. Match the proven code: `controller.Click`. Reach for `Invoke` only when a
click is landing in the wrong place because of DPI or multi-monitor geometry.

## Addressing idioms worth knowing before you start

Roughly in order of preference. Work down the list only when the one above it fails.

**Caption Text, clickable parent.** Toolbar controls in these viewers are usually a group whose
visible caption is a child `Text` element. The Text carries the name; the parent takes the click:

```csharp
IUIAutomationElement caption = _Browser.FindElementOnPage(manager, "Next Case", UIAControlType.Text);
controller.Click(_Browser.GetParent(caption));
```

If `FindElementOnPage(..., UIAControlType.Button)` comes back null for a control you can plainly
see, try `Text` and walk up. This shape drives `NavigateCases`, `SetMagnificationLevel`, and the
layer picker in `SlideMarkup`.

**Material icon ligatures.** Before falling back to any positional walk, check whether the app
uses the Material Icons *font*. The normal markup is `<span class="material-icons">chevron_right</span>` —
the literal string is the element's text, and a font ligature renders it as the glyph.

Where the control has **no accessible name of its own**, UIA promotes that inner text to the
control's own Name. So the element is found directly — no child lookup, no `GetParent`:

```csharp
IUIAutomationElement chevron = _Browser.FindElementOnPage(manager, "chevron_right", UIAControlType.Hyperlink);
controller.Click(chevron);
```

Pass the ControlType the icon actually is — Concentriq's slide chevrons and its return-to-worklist
control are `Hyperlink`, icon buttons elsewhere are `Button`. A control that *does* carry its own
name (`"Viewer Settings"`, `"Rotate Image"`) keeps the ligature as a hidden child instead, so
address those by their real name and ignore the ligature.

Proscia Concentriq is built this way throughout — `chevron_left`, `chevron_right`, `more_vert`,
`expand_more`, `zoom_in`, `rotate_90_degrees_ccw`, `grid_off`, `exposure`, `collections`. Its
next/previous slide arrows are unnamed links and would otherwise need a sibling walk; the
ligature makes them findable by name.

To check a new system, read the accessibility tree and scan for lowercase_snake_case text nodes
that name a *shape* rather than a *function*. If they are there, the icon buttons are addressable.

Three caveats, all of which have teeth:

- **Ligature names are not unique.** `expand_more` sits on every dropdown in Concentriq — the
  user menu, Zoom, Annotations, Grid Settings, Image Filters, the magnification combobox. A
  page-wide search returns whichever comes first, which is meaningless. Use this only for a
  ligature that is rare in the current view, or scope the search to a subtree.
- **It is a rendering detail, not a contract.** An `aria-label` is a deliberate promise; a
  ligature is a side effect of the icon-font strategy. A migration to inline SVG or Material
  Symbols removes every one of these names at once. Say in a comment where the name came from,
  so whoever debugs it later knows what changed.
- **It works because the vendor skipped an accessibility best practice.** A decorative icon span
  is supposed to be `aria-hidden="true"` with a real label on the button. An app that does it
  properly gives you a named button and you never need this; an app that does neither gives you
  nothing. This is the middle case only.

One caution about how this was learned: the Chromium accessibility tree shows the ligature as a
*child* of the link, which reads as though it needs the caption-Text-plus-`GetParent` idiom. UIA
does not present it that way. This is exactly the reason positional structure has to be confirmed
against UIA rather than the browser — the name was right, the shape around it was not.

**Anchor on a name that carries a hotkey.** When the control you want has no usable accessible
name, anchor off a nearby one that does. The best anchors are buttons whose names include their
keyboard shortcut — `"Viewer information (F1)"`, `"Toggle macro image (M)"`,
`"Negative (SHIFT + 7)"` — because the vendor sets those deliberately and they churn least:

```csharp
IUIAutomationElement anchor = _Browser.FindElementOnPage(manager, "Toggle macro image (M)", UIAControlType.Button);
IUIAutomationElement target = _Browser.GetNextSiblingElement(anchor);
```

Always comment what the hops mean. A bare `GetPreviousSiblingElement(x, 3)` is unmaintainable.

Where the vendor exposes no names at all — an icon-only tool tray — let the *named list* carry a
sibling offset instead of a name, and have the script parse it. Adding a tab then means editing
the named list, not the script:

```csharp
int offset = int.Parse(SpeechParams.TranslateSingle(SpeechParams.Names[0]));
IUIAutomationElement group = _Browser.GetPreviousSiblingElement(anchor, offset);
controller.Click(_Browser.GetChild(group));
```

Values, focus, state:

```csharp
SetElementText(element, text)  GetElementText(element)
FocusElement(controller, e)  FocusElementByName(manager, name)  GetFocusedElementAndEnsureOn()
GetToggleState(e)  IsChecked(e)  ToggleExpandCollapse(e)
SetRangeValue(e, v)  SetSliderValue(slider, v)  SetSpinnerValue(spinner, v)
ScrollIntoViewIfPossible(e)  ScrollIntoViewAndWait(e, timeoutMs)
```

Tabs and windows:

```csharp
ActivateTabByCaption(...)  SwitchToTabByName(...)
FocusTabItemByName(document, "Accession Report")  FocusTabItem(tab)  IsTabSelected(tab)
FindChromeWindowByTitle(...)  BringToForeground(window)
```

Diagnostics — the fastest way to learn a new vendor's tree:

```csharp
_Browser.DumpTree(document, maxDepth: 6);   // writes the subtree to StatusLog
_Browser.DescribeElement(element);
```

## Inspecting a new vendor: what to use for what

Three tools see overlapping but different things. Using the wrong one for the wrong question is
how a script ends up finding the element and clicking the one next to it.

**Claude in Chrome** (`read_page`, `find`, `get_page_text`) — reconnaissance. Chromium builds one
accessibility tree and exposes it to Windows UIA, so roles and names mostly correspond:
`button` → `Button`, `link` → `Hyperlink`, `tab` → `TabItem`, `StaticText` → `Text`. Use it to
find the exact spelling of a caption, check whether a control has an accessible name *at all*
(which is what decides between find-by-name and an anchor walk), enumerate which tabs are really
tabs, and read the page title that the window caption is built from. Drive the app to the right
state first — open a case, expand the tray — since most of these scripts run against an open case,
not the worklist.

**FlaUInspect** — the UIA tree itself: ControlType, and the true parent/sibling structure.

**`_Browser.DumpTree`** — ground truth. It walks the identical tree the script will, through the
same helpers, and writes it to StatusLog. Costs one throwaway script run.

The division that matters: **never derive positional logic from the browser.** These scripts walk
UIA's ControlView, which prunes and groups differently from the DOM, so
`GetNextSiblingElement(anchor, 3)`, `GetParent` on a caption, and the offset trick in Halo's
`ShowTab` cannot be counted off a page read — the count seen in Chrome is not the count the script
gets. Names and roles from Chrome; geometry from FlaUInspect or `DumpTree`.

## `_Window` helpers

For systems reached by URL rather than by clicking the worklist:

```csharp
_Window.FindWindowByTitleFragment(fragment)
_Window.OpenInNewChromeWindow(url, titleFragment, timeoutMs)
_Window.NavigateWindowTo(hwnd, url)
_Window.WaitForWindowTitleToContain(hwnd, fragment, timeoutMs)
_Window.MoveWindowToMonitor(hwnd, monitorNumber)   // 1-based, 0 = leave it alone
WindowTools.EnsureForegroundWindow(hwnd)
```

The `EnsureWindow` pattern in `_Flexlis.cs` wraps these: reuse the window for this surface if it
exists, otherwise open one, then confirm the title carries the case number before moving on.
That keeps one window per surface instead of one per case.

## Keystrokes

```csharp
string windowCaption = _<System>.FindCurrentTitlePageByRegex(Application, pattern);
WindowTools.EnsureForegroundWindow(windowCaption);   // do this FIRST

InputSimulator sim = new InputSimulator();
sim.Keyboard.KeyPress(VirtualKeyCode.NEXT);                          // PageDown
sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.HOME);
Automation.Send("45");  Automation.Send("{ENTER}");
_Browser.Send(VirtualKeyCode.TAB);
WindowTools.Wait(200);
```

Keystrokes go to whatever has focus. A keystroke-driven command that does not call
`WindowTools.EnsureForegroundWindow` first will silently drive whatever the pathologist last
clicked. It takes the window caption (a string) or an `IntPtr`.

Keystroke-only commands need no `BrowserManager` at all — `NavigateSlides` in Halo is six lines:
resolve the caption, foreground it, press the key.

## Gotchas that have already cost time

- **`IsElementVisible` only tests presence.** In Halo the "Add Assay" button stays in the
  accessibility tree while its tab is inactive, so `if (!IsElementVisible(...)) openTab();`
  never opened the tab and the script then drove a control that wasn't rendered. Either click
  the toggle unconditionally, or use `IsVisiblyRendered`.
- **A collapsed tool tray hides every tab icon.** When the right-hand tray is minimized, none of
  the tab toggles resolve. Check for the maximize control (e.g. `"Maximize tool tray (T)"`)
  first and click it before looking for a specific tab.
- **Sleeps do not fix races here.** When a script fails intermittently, the cause is usually a
  wrong or ambiguous element reference, not timing. Reach for `WaitForElement`, then
  `DumpTree`, before adding sleeps.
- **SPA titles are set after the route mounts.** Do not assume a fixed sleep is enough after
  navigation; wait on the title.
