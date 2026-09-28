# Driving a desktop LIS

For an LIS client that is a Windows application rather than a web page (PowerPath, CoPath). The counterpart of
`browser-api.md`. Written from the PowerPath work in `demo\sales`, 2026-09-28.

## What is available

- **`WindowTools`** (ships with PRO): `FindWindow(captionRegex)`, `EnsureForegroundWindow(regex | hWnd)`,
  `IsForegroundWindow(regex)`, `GetForegroundWindow()`, `GetWindowText(hWnd)`, `EnumChildWindows(hWnd)`,
  `EnumWindows()`, `GetWindowProcessID(hWnd)`, `WaitForWindow(regex, ms)`, **`WaitForClose(regex, ms)`**, `Wait(ms)`.
  In an `ExtensionScript` it is `WindowTools.Instance`.
- **`Automation`** (AutoIt-style): `Send(keys, waitMs)`, `ClipSet`, `ControlClick(title, "[CLASS:X; INSTANCE:n]")`,
  `ControlGetText(title, control)`, `ControlCommand`, `ControlFocus`. There is **no** `ControlSetText`.
- **`VOScript.Starter._Desktop`** (tenant library): a UIA-based `DesktopManager` (by process), element finding by
  AutomationId, `WaitForDialog`, `DismissDialog`, `DumpTree`. Good for modern apps with proper UIA; a Delphi or VB6
  client usually exposes little through UIA, and Win32 handles plus `Automation.Control*` work better there.

Scripts read the client's own assemblies, so the exact surface can be reflected from
`...\PRO_Server\Production\Client\VoiceOver.Extensions.dll`.

## Poll the real state - never a fixed wait, never a blind keystroke

This is the lesson the PowerPath rewrite was built on, and every LIS template follows it.

- **Wait for the thing, not for time.** After a keystroke that opens a window or a case, loop on `FindWindow` /
  the window title every 100 ms against a deadline, instead of `Wait(2000)` for something that usually never
  appears. PowerPath's old DictateSection spent ~6 s per call on waits for prompts that almost never showed.
- **Only answer a prompt that is showing.** `Automation.Send("!y")` sent "in case a dialog is up" lands on the LIS
  itself when it is not. Check `IsForegroundWindow(title)` - or that the foreground window belongs to the LIS's
  process and is not its main window - first.
- **Confirm the action worked before the next step.** CaseNumber waits until the LIS actually shows the requested
  case; otherwise a misheard number dictates into whatever case was open.
- **Handle prompts in whatever order they come.** One loop that reacts to each known prompt title beats a sequence of
  "wait for spelling, then wait for ICD, then wait for status".
- **Every early return logs why.** A silent `return` is indistinguishable from a broken command. PowerPath's NextCase
  returned silently while the Word report was still closing, which made CaseComplete look intermittent.

## Reading a desktop client without touching it

Delphi clients (PowerPath) have two quirks worth knowing:

- The process's `MainWindowHandle` can be a hidden `TApplication` stub titled like the app. Enumerate the process's
  **visible** top-level windows instead; PowerPath's real frame is `TfrmMain`, titled
  `PowerPath Client - [Case Information - S-26-00026]` when a case window is maximized.
- AutoIt control IDs (`[CLASS:TttDBLookupCombo; INSTANCE:2]`) are class name plus 1-based order in
  `EnumChildWindows`. To map them, dump each child's class, instance, visibility, enabled state, rectangle and
  `WM_GETTEXT` from PowerShell (`Add-Type` over `user32`). Labels in Delphi forms are non-windowed, so they will not
  appear - position tells you which combo is which.

Always do this read-only, against a dialog the user has opened, rather than driving the application to get there.

## The LIS's own logic comes first

PowerPath has a case-status progression: Case Status opens with the next step already chosen for the current status.
A script that types a status over it fights the application - a `NextStatus` that is not a step in the progression is
silently ignored and the pre-filled step stays. Read what the application proposes before overriding it.

## Where it goes

The scaffolded `_<System>` library holds every one of these application-specific pieces - window patterns, how the
open case is shown, the keystrokes that open, save and clear one. The Core commands hold the workflow and do not
change per LIS.
