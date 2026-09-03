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
    [ExtensionCommandClass(HelpText = "Sets the viewer magnification from the spoken level. Trigger: zoom, followed by the {{System}}Magnifications named list.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class SetMagnificationLevel : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will set the magnification of the {{Vendor}} slide viewer using the
            // magnification controls on the viewer toolbar.
            //
            // *****

            // The trigger is "zoom <{{System}}Magnifications>", so the invariant word lives in the
            // trigger and the named list carries ONLY the part that varies - "20x", "In", "Reset",
            // never "Zoom to 20x". If the control's on-screen caption needs more than the spoken
            // value, compose it here rather than repeating the invariant text on every list row.
            //
            // TODO: if this vendor's captions are not the bare magnification, add the composition,
            // e.g. menuItem = "Zoom to " + magnificationLevel, with explicit cases for any level
            // the vendor words differently.
            string magnificationLevel = SpeechParams.TranslateSingle(SpeechParams.Names[0]);

            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                // TODO: confirm the toolbar structure, and that the named list's translations
                // match the captions exactly ("2x", "10x", "Fit").
                //
                // The zoom controls are usually a group whose caption is a child Text element:
                // find the Text, click its parent. If the buttons themselves are named, search
                // for Button and drop the GetParent.
                IUIAutomationElement caption = _Browser.FindElementOnPage(manager, magnificationLevel, UIAControlType.Text);

                if (caption == null)
                    throw new ClientException($"Could not find the \"{magnificationLevel}\" zoom control. Check the {{System}}Magnifications named list against the viewer toolbar.");

                controller.Click(_Browser.GetParent(caption));
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
            }

            // ---------------------------------------------------------------------------
            // Keyboard variant, for viewers that bind number keys to fixed zoom steps. Faster
            // and does not need the toolbar rendered, but the viewer must have focus.
            //
            // WindowTools.EnsureForegroundWindow(titlePage);
            // InputSimulator sim = new InputSimulator();
            // switch (magnificationLevel)
            // {
            //     case "Fit": sim.Keyboard.KeyPress(VirtualKeyCode.VK_1); break;
            //     case "2x":  sim.Keyboard.KeyPress(VirtualKeyCode.VK_2); break;
            //     case "4x":  sim.Keyboard.KeyPress(VirtualKeyCode.VK_3); break;
            //     case "10x": sim.Keyboard.KeyPress(VirtualKeyCode.VK_4); break;
            //     case "20x": sim.Keyboard.KeyPress(VirtualKeyCode.VK_5); break;
            //     case "40x": sim.Keyboard.KeyPress(VirtualKeyCode.VK_6); break;
            //     default:
            //         StatusLog.WriteWarningEntry($"Magnification \"{magnificationLevel}\" has no mapping.");
            //         break;
            // }
            // ---------------------------------------------------------------------------

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
