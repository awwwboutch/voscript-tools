// SOURCE: VOScript.Starter.Halo.CaseNumber@5P-3525-7.xml
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
using VOScript.Starter.EpicMock;
#endregion USING NAMESPACES

namespace VOScript.Starter.Halo
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseNumber : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 12/17/2025
            // Use:
            // This will search for the specified case in Indica Halo's worklist
            //
            // *****
             
            string caseNumber = _CaseNumber.FormatParameterizedSpeech(SpeechParams, 6, "HaloCaseType", true);
            Application.SetStateProperty("CaseNumber", caseNumber);

            string titlePage = _Halo.FindCurrentTitlePage(Application);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                //setting the filter pulldown to accession number before searching
                IUIAutomationElement casesText = _Browser.FindElementOnPage(manager, "Cases", UIAControlType.Text);
                IUIAutomationElement pulldownList = _Browser.GetNextSiblingElement(casesText);
                _Browser.SetElementText(pulldownList, "Accession #");
                WindowTools.Wait(150);
                
                IUIAutomationElement searchField = _Browser.FindElementOnPage(manager, "Search cases", UIAControlType.Edit);
                _Browser.SetElementText(searchField, caseNumber);
                WindowTools.Wait(150);
                
                IUIAutomationElement caseNumberLink = _Browser.FindElementOnPage(manager, caseNumber, UIAControlType.Hyperlink);
                controller.Click(caseNumberLink);
                
                if (WindowTools.FindWindow("Voicebrook - Demo AP System") != IntPtr.Zero)
                    EpicMock.CaseNumber_Logic.Execute(Application, SpeechParams, DocumentStore, SpeechBox, caseNumber);
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




