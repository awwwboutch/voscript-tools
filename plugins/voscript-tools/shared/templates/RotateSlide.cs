#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
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
    [ExtensionCommandClass(HelpText = "Rotates the slide to the spoken number of degrees. Trigger: the word rotate followed by up to three spoken digits")]
    [ExtensionStringProperty("RotateToggleName", DefaultValue = "Toggle rotator", HelpText = "Control that opens the rotation gadget.")]
    [ExtensionBoolProperty("CloseAfterRotate", DefaultValue = true, HelpText = "Close the rotation gadget when finished.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class RotateSlide : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will rotate the slide in the {{Vendor}} viewer to the spoken value.
            //
            // *****

            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            IUIAutomationElement rotateToggle = null;

            try
            {
                // TODO: confirm the rotation gadget's structure, and try SetElementText FIRST.
                // Both shapes are in production: Concentriq's is a Spinner whose accessible name
                // is the degree symbol and it takes SetElementText directly, while AISight's
                // exposes no ValuePattern and has to be clicked into and typed. Reach for the
                // typing route below only after SetElementText is shown not to work.

                rotateToggle = _Browser.FindElementOnPage(manager, Property("RotateToggleName", "Toggle rotator"), UIAControlType.Button);
                controller.Click(rotateToggle);
                WindowTools.Wait(150);

                // TODO: replace this anchor walk. The rotation input is usually unreachable by
                // name, so anchor off a nearby button that IS named - preferably one whose name
                // includes its hotkey, since those are set deliberately by the vendor and churn
                // least - then walk to the input. Use _Browser.DumpTree to find the shortest
                // walk that works, and leave a comment saying what the hops mean.
                IUIAutomationElement anchor = _Browser.FindElementOnPage(manager, "Fit to viewport", UIAControlType.Button);
                IUIAutomationElement anchorParent = _Browser.GetParent(anchor);
                IUIAutomationElement rotateGroup = _Browser.GetPreviousSiblingElement(anchorParent, 3);

                controller.Click(rotateGroup);
                WindowTools.Wait(150);

                Automation.Send(SpeechParams.TranslateSingle(SpeechParams.Names[0]));
                Automation.Send("{ENTER}");
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
            }
            finally
            {
                // Close the gadget, or the viewer is left in a modal state on failure.
                if (rotateToggle != null && Property("CloseAfterRotate", true))
                {
                    try { controller.Click(rotateToggle); }
                    catch (Exception ex) { StatusLog.WriteWarningEntry($"Could not close the rotation gadget: {ex.Message}"); }
                }
            }

            // ---------------------------------------------------------------------------
            // Variant for viewers with no rotation input at all, only incremental rotate
            // hotkeys (Halo: O and P, 5 degrees per press). Read the current rotation off the
            // gadget's own caption, compute the delta, and press the key that many times.
            //
            // double.TryParse(SpeechParams.TranslateSingle(SpeechParams.Names[0]), out double desired);
            //
            // IUIAutomationElement rotationAnchor = _Browser.FindElementOnPage(manager, "Toggle macro image (M)", UIAControlType.Button);
            // IUIAutomationElement sliderValue = _Browser.GetNextSiblingElement(rotationAnchor);
            // IUIAutomationElement subGroup = _Browser.GetChild(sliderValue);
            // double.TryParse(subGroup.CurrentName.Replace("°", ""), out double current);
            //
            // double increments = (int)Math.Round((desired - current) / 5.0);
            // InputSimulator sim = new InputSimulator();
            // for (int i = 0; i < Math.Abs(increments); i++)
            // {
            //     if (increments < 0) sim.Keyboard.KeyPress(VirtualKeyCode.VK_P);
            //     else                sim.Keyboard.KeyPress(VirtualKeyCode.VK_O);
            //     System.Threading.Thread.Sleep(2);
            // }
            //
            // This lands on the nearest 5-degree step, not the exact value. If the viewer wraps
            // at 360, normalize the delta with ((desired - current + 540) % 360) - 180 so a
            // 10-degree move never becomes a 350-degree one.
            // ---------------------------------------------------------------------------

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
