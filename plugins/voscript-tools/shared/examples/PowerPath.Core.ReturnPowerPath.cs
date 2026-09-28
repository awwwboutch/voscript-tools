#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Extensions;
using System.Windows;
using System.Text.RegularExpressions;
using VoiceOver.Common;
using VOScript.Standard.SpeechBox;
#endregion USING NAMESPACES

namespace VOScript.Starter.PowerPath.Core
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
  [ExtensionCommandClass(HelpText = "This command saves the word report and optionally answers the spell check, ICD, status update, and print draft prompts depending on site config and preference.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class ReturnPowerPath : CommandScript
    {
        public override void Execute()
        {
            ReturnPowerPath_Logic.Execute(Application, SpeechBox, DocumentStore, SpeechParams);
        }
    }
    
    public class ReturnPowerPath_Logic : ExtensionScript
    {
        const string CaseStatusWindow = "Case Status";
        const string NextStepCombo = "[CLASS:TttDBLookupCombo; INSTANCE:2]";   //Progress status > new step
        const string StatusOkButton = "[CLASS:TButton; INSTANCE:2]";
        
        //Statuses this command must never set: they sign the case out, which is always done by hand
        static readonly string[] SignoutStatuses = { "Final", "Addendum Final", "Amendment Final", "Correction Final" };
        
        /// <summary>
        /// Returns true once the report is saved back to PowerPath - including when the status update was backed out
        /// because the next step would sign the case out. Returns false when the case is left for the user to finish
        /// (Report Builder still open, spell check, ICD prompt, Case Status dialog) or nothing was saved, so a combined
        /// command like CaseComplete stops instead of moving to the next case.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore, SpeechParameters SpeechParams)
        {
            WindowTools WindowTools = WindowTools.Instance;
            
            //only relevant for premium implementations
            if (SpeechBox.ActiveDocument != null) 
            {
                WindowTools.EnsureForegroundWindow("Report Builder");
                return false;
            }
            
            //Optional override of the status to advance to. Blank = accept the next step PowerPath's own case progression
            //proposes for the current status.
            string nextStatus = SpeechParams.Names.Contains("NextStatus") ? SpeechParams.TranslateSingle("NextStatus") : Application.GetStateProperty("NextStatus");
            
            IntPtr word = WindowTools.FindWindow(_PowerPath.WordTitlePattern);
            if (word == IntPtr.Zero)
            {
                StatusLog.WriteInformationEntry("ReturnPowerPath - no Word report open, nothing to save");
                return false;
            }
            
            bool spellCheckByUser = Application.GetStateProperty("SpellCheckPreference") == "True";
            bool answerIcdYes = Application.GetStateProperty("ICDPreference") == "True";
            bool updateStatus = Application.GetStateProperty("UpdateStatus") == "True";
            
            //Initializing the save process in PowerPath
            WindowTools.EnsureForegroundWindow(word);
            _PowerPath.SaveCase();
            
            //Handle PowerPath's save prompts in whatever order they appear, rather than a fixed wait for each prompt the
            //site might have. Spelling / ICD are handled whenever they show; SpellCheckConfig / ICDConfig no longer
            //need to be set for that.
            int spellingCloses = 0;
            DateTime deadline = DateTime.Now.AddSeconds(10);
            while (DateTime.Now < deadline)
            {
                if (WindowTools.IsForegroundWindow("^Spelling: "))
                {
                    if (spellCheckByUser || spellingCloses >= 20) return false;     //user runs the spell check themselves
                    Automation.Send("!{F4}", 200);
                    WindowTools.WaitForClose("^Spelling: ", 1000);
                    spellingCloses++;
                    deadline = DateTime.Now.AddSeconds(10);
                    continue;
                }
                if (WindowTools.IsForegroundWindow("^Save Option"))     //ICD code prompt
                {
                    if (answerIcdYes)
                    {
                        Automation.Send("!y");
                        return false;       //user goes on to enter ICD codes
                    }
                    Automation.Send("!n");
                    WindowTools.WaitForClose("^Save Option", 1000);
                    deadline = DateTime.Now.AddSeconds(10);
                    continue;
                }
                if (WindowTools.IsForegroundWindow("^Update Status"))
                {
                    Automation.Send("!y");     //answering "yes" to update status prompt
                    
                    //If UpdateStatus property set to false, command will exit and ClickYesNo will be used to complete process.
                    return updateStatus && SetCaseStatus(nextStatus);
                }
                WindowTools.Wait(100);
            }
            
            StatusLog.WriteErrorEntry("ReturnPowerPath - Update Status prompt did not appear after saving the report");
            return false;
        }
        
        /// <summary>
        /// Completes the Case Status dialog. With nextStatus blank, PowerPath's pre-filled next step is accepted; otherwise
        /// nextStatus is entered as an override. A step that would sign the case out is always backed out of (Cancel) -
        /// sign-out is done by hand - and counts as success: the report is saved, only the status is left alone. Returns
        /// false (dialog left for the user) when an override didn't take or the dialog wouldn't close.
        /// </summary>
        static bool SetCaseStatus(string nextStatus)
        {
            WindowTools WindowTools = WindowTools.Instance;
            if (!WindowTools.WaitForWindow("^" + CaseStatusWindow, 5000))
            {
                StatusLog.WriteErrorEntry("ReturnPowerPath - Case Status dialog did not open");
                return false;
            }
            
            if (!string.IsNullOrEmpty(nextStatus))
            {
                Automation.ClipSet(nextStatus);
                WindowTools.Wait(150);
                Automation.Send("{HOME}+{END}", 50);
                Automation.Send("^v", 150);
                Automation.Send("{TAB}", 200);
            }
            string newStatus = (Automation.ControlGetText(CaseStatusWindow, NextStepCombo) ?? "").Trim();
            
            //never sign a case out from here: back out of the status update and let the chain carry on
            if (SignoutStatuses.Any(s => s.Equals(newStatus, StringComparison.OrdinalIgnoreCase)))
            {
                Automation.Send("!c");
                WindowTools.WaitForClose("^" + CaseStatusWindow, 3000);
                StatusLog.WriteInformationEntry("ReturnPowerPath - next step '" + newStatus + "' would sign the case out; status update cancelled, report saved");
                return true;
            }
            
            //Override didn't take (typo in NextStatus, or not a step in PowerPath's progression from the current status): leave it for the user
            if (!string.IsNullOrEmpty(nextStatus) && !newStatus.StartsWith(nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                StatusLog.WriteInformationEntry(string.Format("ReturnPowerPath - Case Status shows '{0}', expected '{1}'; left for the user", newStatus, nextStatus));
                return false;
            }
            
            Automation.ControlClick(CaseStatusWindow, StatusOkButton);
            if (!WindowTools.WaitForClose("^" + CaseStatusWindow, 3000))
            {
                StatusLog.WriteInformationEntry("ReturnPowerPath - Case Status dialog still open after OK");
                return false;
            }
            StatusLog.WriteInformationEntry("ReturnPowerPath - case status set to '" + newStatus + "'");
            return true;
        }
        
#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
