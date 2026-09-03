// SOURCE: VOScript.Starter.Halo.RotateSlide@2P-3392-3.xml
// Published reference example, extracted 2026-09-01 from demo\sales.
// Read-only: do not edit. See examples/README.md for what each one demonstrates.

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
using System.Text.RegularExpressions;
#endregion USING NAMESPACES

namespace VOScript.Starter.Halo
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class RotateSlide : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 12/17/2025
            // Use:  This script will rotate the slide to the desired rotation level by looking at the current level and then pressing P or O the right number of times
            // to get to the approximate rotation value
            //
            //
            // *****

            double.TryParse(SpeechParams.TranslateSingle(SpeechParams.Names[0]), out double currentValue);

            string titlePage = _Halo.FindCurrentTitlePageByRegex(Application, @"^[A-Z]{1,2}-[0-9]{2}-[0-9]{1,6}");
            var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                IUIAutomationElement rotationAnchor = _Browser.FindElementOnPage(manager, "Toggle macro image (M)", UIAControlType.Button);
                IUIAutomationElement rotationSliderValue = _Browser.GetNextSiblingElement(rotationAnchor);
                IUIAutomationElement subGroup = _Browser.GetChild(rotationSliderValue);
                double.TryParse(subGroup.CurrentName.Replace("°", ""), out double desiredValue);
                
                //double delta = ((desiredValue - currentValue + 540) % 360) - 180;
                double delta = (desiredValue - currentValue);

                // number of 5-degree increments
                double increments = (int)Math.Round(delta / 5.0);
                StatusLog.WriteWarningEntry(increments.ToString());
                
                InputSimulator sim = new InputSimulator();
                
                for (int i = 0; i < Math.Abs(increments); i++)
                {
                    if (increments < 0)
                        sim.Keyboard.KeyPress(VirtualKeyCode.VK_P);
                    else
                        sim.Keyboard.KeyPress(VirtualKeyCode.VK_O);
                    System.Threading.Thread.Sleep(2);
                }
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling Halo: {ex.Message}", ex);
            }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE





