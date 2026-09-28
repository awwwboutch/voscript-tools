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
    [ExtensionCommandClass(HelpText = "Writes the Report Builder text into the {{Vendor}} Word report. Trigger: send report")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class ReturnToWord : CommandScript
    {
        public override void Execute()
        {
            ReturnToWord_Logic.Execute(Application, SpeechBox, DocumentStore);
        }
    }

    public class ReturnToWord_Logic : ExtensionScript
    {
        /// <summary>
        /// Returns false when nothing was transferred (validation declined, Word report closed, a header missing), so
        /// CaseComplete stops before saving the case back to {{Vendor}}. On false the document is NOT released - the
        /// dictation is still in Report Builder and the pathologist can fix and retry.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore)
        {
            // Author: {{Author}}
            // Date: {{Date}}

            IDocument document = SpeechBox.ActiveDocument;
            if (document == null)
            {
                StatusLog.WriteWarningEntry("ReturnToWord - no Report Builder document is open");
                return false;
            }

            //Run a validation check and prompt user if validation errors are found
            bool allowOverride = !"false".Equals(Application.GetStateProperty("AllowValidationOverride"), StringComparison.OrdinalIgnoreCase);
            if (!DefaultValidation.TestActiveDocument(SpeechBox, Application, DocumentStore, allowOverride)) return false;
            SpeechBox.SetInputFocus(SpeechBox.GetNextFocus(SpeechBox.InputFocus));  //moving out of the current field strips its trailing whitespace

            //Make sure the Word report for THIS case is the one open before touching it
            IntPtr word = _{{System}}.FindWordReport(document.CaseNumber);
            if (word == IntPtr.Zero)
            {
                StatusLog.WriteInformationEntry("ReturnToWord - Word report for " + document.CaseNumber + " is not open");
                _{{System}}.ShowWarning(Application, string.Format("It appears that the Word report for case {0} has been closed.\n\nPlease re-open it and issue this command again.", document.CaseNumber));
                return false;
            }
            WindowTools.Instance.EnsureForegroundWindow(word);

            //Section layout is {{Vendor}}'s (_{{System}}); undo, protection and Word error capture are VOScript.Starter._Word
            if (!_{{System}}.TransferReportToWord(Application, word, document)) return false;

            document.MarkEvent("Report successfully transferred to AP System");
            _Premium.UpdateStage(Application, SpeechBox);

            if (DocumentStore.SaveAndFinalize(document)) SpeechBox.Close();
            Application.SetCommandDocument("{{ReportTemplate}}");
            return true;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
