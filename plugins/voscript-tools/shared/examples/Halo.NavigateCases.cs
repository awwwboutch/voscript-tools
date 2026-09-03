// SOURCE: VOScript.Starter.Halo.NavigateCases@2P-3389-3.xml
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
using System.Windows.Automation;
using WindowsInput;                 // For InputSimulator
using WindowsInput.Native;          // For InputSimulator
using BrowserManager = VOScript.Starter.Browser.Manager.BrowserManager;
using System.Runtime.InteropServices;
#endregion USING NAMESPACES

namespace VOScript.Starter.Halo
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class NavigateCases: CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 11/4/2025
            // Use:
            // This will use the next or previous case button found in the slide viewer in Halo
            //
            // *****

            string titlePage = _Halo.FindCurrentTitlePageByRegex(Application, @"^[A-Z]{1,2}-[0-9]{2}-[0-9]{1,6}");
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                IUIAutomationElement slideButton = (SpeechParams.TranslateSingle(SpeechParams.Names[0]).ToLower() == "next") ?
                    _Browser.FindElementOnPage(manager, "Next Case", UIAControlType.Text) :
                    _Browser.FindElementOnPage(manager, "Previous Case", UIAControlType.Text);
                controller.Click(_Browser.GetParent(slideButton));
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




