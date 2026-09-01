#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UIAutomationClient;           // For BrowserController
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class NavigateSlides : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will send the PageUp / PageDown hotkeys to move between slides in the
            // {{Vendor}} slide viewer.
            //
            // *****

            // TODO: confirm which keys {{Vendor}} binds to slide navigation. PageDown/PageUp and
            // Down/Up are both in use across the shipped integrations.

            string windowCaption = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);

            // Keystrokes go wherever focus happens to be. Foreground the viewer first, or the
            // command silently drives whatever the pathologist last clicked.
            WindowTools.EnsureForegroundWindow(windowCaption);

            InputSimulator sim = new InputSimulator();

            if (SpeechParams.TranslateSingle(SpeechParams.Names[0]).ToLower() == "next")
                sim.Keyboard.KeyPress(VirtualKeyCode.NEXT);   // PageDown
            else
                sim.Keyboard.KeyPress(VirtualKeyCode.PRIOR);  // PageUp

            // If {{Vendor}} only accepts slide navigation while the slide tray is open, open it
            // first. Click the toggle unconditionally rather than guarding on IsElementPresent -
            // that helper only tests presence in the tree, and an inactive panel's controls stay
            // in the tree, so the guard passes and the click is skipped.
            //
            // using var manager = new Browser.Manager.BrowserManager(windowCaption);
            // using var controller = manager.GetBrowserController();
            // IUIAutomationElement trayToggle = _Browser.FindElementOnPage(manager, "Image library", UIAControlType.Button);
            // if (trayToggle != null) { controller.Click(trayToggle); WindowTools.Wait(150); }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
