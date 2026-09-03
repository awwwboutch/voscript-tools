// SOURCE: VOScript.Starter.Concentriq.NavigateSlides@1P-2981-3.xml
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

namespace VOScript.Starter.Concentriq
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class NavigateSlides : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 11/4/2025
            // Use:
            // This will
            //
            // *****

            string titlePage = _Concentriq.FindCurrentTitlePage(Application);
            var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                IUIAutomationElement document = controller.FindWebPageDocument(titlePage);

                IUIAutomationElement slideButton = (SpeechParams.TranslateSingle(SpeechParams.Names[0]).ToLower() == "next") ? 
                    _Browser.FindElementOnPage(manager, "chevron_right", UIAControlType.Hyperlink) :
                    _Browser.FindElementOnPage(manager, "chevron_left", UIAControlType.Hyperlink);
                controller.Click(slideButton);
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling Concentriq: {ex.Message}", ex);
            }
            finally
            {
                // Must dispose to avoid memory leaks and/or crashes
                controller?.Dispose();
            }


        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE



