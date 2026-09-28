#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using VOScript.Standard.SpeechBox;
#endregion USING NAMESPACES

namespace {{CoreNamespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
//@if Word
    [ExtensionCommandClass(HelpText = "Saves the Word report back into {{Vendor}} and completes the status update. Never signs a case out. Trigger: return to {{Vendor}}")]
//@else
    [ExtensionCommandClass(HelpText = "Writes the Report Builder text into the {{Vendor}} report. Trigger: send report")]
//@endif
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class {{ReturnToName}} : CommandScript
    {
        public override void Execute()
        {
//@if Word
            {{ReturnToName}}_Logic.Execute(Application, SpeechBox, SpeechParams);
//@else
            {{ReturnToName}}_Logic.Execute(Application, SpeechBox, DocumentStore);
//@endif
        }
    }

    public class {{ReturnToName}}_Logic : ExtensionScript
    {
//@if Word
        // TODO: every value in this block is PowerPath's, as a starting point - confirm each against {{Vendor}}.
        const string SaveKeys = "{F10}";                                        //saves the Word report back into the LIS
        const string StatusPromptPattern = "^Update Status";                    //"update the case status?" prompt
        const string StatusDialogPattern = "^Case Status";                      //the status dialog itself
        const string StatusDialogTitle = "Case Status";                         //its title, for Automation.Control* calls
        const string NextStepControl = "[CLASS:TttDBLookupCombo; INSTANCE:2]";   //pre-filled with the LIS's next step
        const string StatusOkControl = "[CLASS:TButton; INSTANCE:2]";

        /// <summary>
        /// Returns true once the report is saved back into {{Vendor}} - INCLUDING when the status update was backed out
        /// because the next step would sign the case out: the report is saved, only the status is left, and CaseComplete
        /// carries on to NextCase. Returns false only when the case is left for the user to finish, or nothing was saved.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, SpeechParameters SpeechParams)
        {
            // Author: {{Author}}
            // Date: {{Date}}
            WindowTools WindowTools = WindowTools.Instance;

            if (SpeechBox.ActiveDocument != null)
            {
                StatusLog.WriteInformationEntry("{{ReturnToName}} - Report Builder is still open; send the report first");
                return false;
            }

            IntPtr word = _{{System}}.FindWordReport();
            if (word == IntPtr.Zero)
            {
                StatusLog.WriteInformationEntry("{{ReturnToName}} - no Word report open, nothing to save");
                return false;
            }

            //Optional override of the status to move to. Blank = accept the next step {{Vendor}}'s own case progression
            //proposes. Either way a sign-out step is never accepted.
            string nextStatus = SpeechParams.Names.Contains("NextStatus") ? SpeechParams.TranslateSingle("NextStatus") : Application.GetStateProperty("NextStatus");
            bool updateStatus = Application.GetStateProperty("UpdateStatus") == "True";

            WindowTools.EnsureForegroundWindow(word);
            Automation.Send(SaveKeys);

            //React to the save prompts in whatever order they come, instead of a fixed wait for each one. TODO: handle any
            //other prompts {{Vendor}} shows here, by title (PowerPath: a Spelling dialog and an ICD "Save Option" prompt).
            DateTime deadline = DateTime.Now.AddSeconds(10);
            while (DateTime.Now < deadline)
            {
                if (WindowTools.IsForegroundWindow(StatusPromptPattern))
                {
                    if (!updateStatus) return false;        //the status prompt is left for the user
                    Automation.Send("!y");
                    return SetCaseStatus(nextStatus);
                }
                if (_{{System}}.FindWordReport() == IntPtr.Zero)
                {
                    StatusLog.WriteInformationEntry("{{ReturnToName}} - report saved; {{Vendor}} did not ask to update the status");
                    return true;
                }
                WindowTools.Wait(100);
            }

            StatusLog.WriteErrorEntry("{{ReturnToName}} - the save did not complete within 10 seconds");
            return false;
        }

        /// <summary>
        /// Completes the status dialog: accepts the pre-filled next step, or enters nextStatus as an override. A step that
        /// would sign the case out is always backed out of (Cancel) and counts as success.
        /// </summary>
        static bool SetCaseStatus(string nextStatus)
        {
            WindowTools WindowTools = WindowTools.Instance;
            if (!WindowTools.WaitForWindow(StatusDialogPattern, 5000))
            {
                StatusLog.WriteErrorEntry("{{ReturnToName}} - the status dialog did not open");
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
            string newStatus = (Automation.ControlGetText(StatusDialogTitle, NextStepControl) ?? "").Trim();

            //never sign a case out from here: back out of the status update and let the chain carry on
            if (_{{System}}.IsSignoutStatus(newStatus))
            {
                Automation.Send("!c");
                WindowTools.WaitForClose(StatusDialogPattern, 3000);
                StatusLog.WriteInformationEntry("{{ReturnToName}} - next step '" + newStatus + "' would sign the case out; status update cancelled, report saved");
                return true;
            }

            //override didn't take (typo, or not a step in the progression from the current status): leave it for the user
            if (!string.IsNullOrEmpty(nextStatus) && !newStatus.StartsWith(nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                StatusLog.WriteInformationEntry(string.Format("{{ReturnToName}} - status shows '{0}', expected '{1}'; left for the user", newStatus, nextStatus));
                return false;
            }

            Automation.ControlClick(StatusDialogTitle, StatusOkControl);
            if (!WindowTools.WaitForClose(StatusDialogPattern, 3000))
            {
                StatusLog.WriteInformationEntry("{{ReturnToName}} - status dialog still open after OK");
                return false;
            }
            StatusLog.WriteInformationEntry("{{ReturnToName}} - case status set to '" + newStatus + "'");
            return true;
        }
//@else
        /// <summary>
        /// Writes every dictated Report Builder section into the matching {{Vendor}} report field. Returns false - and does
        /// NOT release the document - if any section had nowhere to go: the dictation is still in Report Builder and the
        /// pathologist can retry. Releasing a document whose text never landed loses the dictation.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore)
        {
            // Author: {{Author}}
            // Date: {{Date}}

            IDocument document = SpeechBox.ActiveDocument;
            if (document == null)
            {
                StatusLog.WriteWarningEntry("{{ReturnToName}} - no Report Builder document is open");
                return false;
            }

            bool allowOverride = !"false".Equals(Application.GetStateProperty("AllowValidationOverride"), StringComparison.OrdinalIgnoreCase);
            if (!DefaultValidation.TestActiveDocument(SpeechBox, Application, DocumentStore, allowOverride)) return false;
            SpeechBox.SetInputFocus(SpeechBox.GetNextFocus(SpeechBox.InputFocus));  //moving out of the current field strips its trailing whitespace

            if (!_{{System}}.FocusLis())
            {
                _{{System}}.ShowWarning(Application, "{{Vendor}} is not open.  Nothing was sent.");
                return false;
            }

            //matched by the section's own Label, so a section added to the template needs no script change
            List<string> notSent = new List<string>();
            foreach (IDocPart part in _{{System}}.TopLevelSections(document).Where(_{{System}}.HasDictation))
                if (!_{{System}}.WriteReportSection(Application, part)) notSent.Add(part.Label);

            if (notSent.Count > 0)
            {
                StatusLog.WriteWarningEntry("{{ReturnToName}} - no {{Vendor}} field for: " + string.Join(" | ", notSent));
                _{{System}}.ShowWarning(Application, string.Format("These sections could not be sent to {{Vendor}}:\n\n{0}\n\nThe report is still open in Report Builder.", string.Join("\n", notSent)));
                return false;
            }

            document.MarkEvent("Report successfully transferred to AP System");
            _Premium.UpdateStage(Application, SpeechBox);

            if (DocumentStore.SaveAndFinalize(document)) SpeechBox.Close();
            Application.SetCommandDocument("{{ReportTemplate}}");
            return true;
        }
//@endif

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
