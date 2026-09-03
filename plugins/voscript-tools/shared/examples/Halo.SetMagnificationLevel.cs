// SOURCE: VOScript.Starter.Halo.SetMagnificationLevel@2P-3393-3.xml
// Published reference example, extracted 2026-09-01 from demo\sales.
// Read-only: do not edit. See examples/README.md for what each one demonstrates.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using UIAutomationClient;           // For BrowserController
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
#endregion USING NAMESPACES

namespace VOScript.Starter.Halo
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class SetMagnificationLevel : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 7/31/2025
            // Use:
            // This will set the magnification of the slide viewer using the magnification buttons on the bottom toolbar in Halo's slide viewer
            //
            // *****

            string magnificationLevel = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
            
            string titlePage = _Halo.FindCurrentTitlePageByRegex(Application, @"^[A-Z]{1,2}-[0-9]{2}-[0-9]{1,6}");
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();


            try
            {
                //setting the filter pulldown to accession number before searching
                IUIAutomationElement button = _Browser.FindElementOnPage(manager, magnificationLevel, UIAControlType.Text);    
                IUIAutomationElement buttonGroup = _Browser.GetParent(button);
                controller.Click(buttonGroup);
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling Halo: {ex.Message}", ex);
            }
        } // close Execute;

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE










