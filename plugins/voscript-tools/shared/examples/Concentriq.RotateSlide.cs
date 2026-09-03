// SOURCE: VOScript.Starter.Concentriq.RotateSlide@1P-2984-3.xml
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

namespace VOScript.Starter.Concentriq
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
            // Use:
            // 
            //
            // *****

            string rotationValue = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
            
            string titlePage = _Concentriq.FindCurrentTitlePage(Application);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();

            try
            {
                IUIAutomationElement rotationWidget = _Browser.FindElementOnPage(manager, "°", UIAControlType.Spinner);
                IUIAutomationElement rotationButton = _Browser.FindElementOnPage(manager, "Rotate Image", UIAControlType.Button);
                if (!_Browser.IsVisiblyRendered(rotationWidget))
                {
                    controller.Click(rotationButton);
                    System.Threading.Thread.Sleep(150);
                }
                rotationWidget = _Browser.FindElementOnPage(manager, "°", UIAControlType.Spinner);
                _Browser.SetElementText(rotationWidget, rotationValue);
                controller.Click(rotationWidget);
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




