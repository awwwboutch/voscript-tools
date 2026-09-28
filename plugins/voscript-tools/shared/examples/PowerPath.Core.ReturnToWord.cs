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
  [ExtensionCommandClass(HelpText = "This command will render text from SpeechBox and paste into the appropriate sections in Word")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class ReturnToWord : CommandScript
    {
        public override void Execute()
        {
            ReturnWord_Logic.Execute(Application, SpeechBox, DocumentStore);
        }
    }

    public class ReturnWord_Logic : ExtensionScript
    {
        /// <summary>
        /// Returns false when nothing was transferred (validation declined, Word report closed, header missing), so a
        /// combined command like CaseComplete can stop before saving the case back to PowerPath.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore)
        {
            WindowTools WindowTools = WindowTools.Instance;
            IDocument document = SpeechBox.ActiveDocument;

            //Run a validation check and prompt user if validation errors are found
            if (!DefaultValidation.TestActiveDocument(SpeechBox, Application, DocumentStore, Convert.ToBoolean(Application.GetStateProperty("AllowValidationOverride")))) return false;
            SpeechBox.SetInputFocus(SpeechBox.GetNextFocus(SpeechBox.InputFocus));  //moving out of current field to ensure extra whitespace has been stripped from outside of current field.

            //Make sure the Word report for this case is the one open before touching it
            if (!WindowTools.EnsureForegroundWindow("^Case '" + Regex.Escape(document.CaseNumber)))
            {
                StatusLog.WriteInformationEntry("Error - Case not open in AP System");
                SpeechDialogSettings error = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "Unable to find case open in Microsoft Word",
                    string.Format("It appears that the Word Document for case {0} has been closed.\n\nPlease re-open the Word document and reissue this command again.", document.CaseNumber));
                error.AddChoice("OK", "Say \"OK\" to continue.");
                Application.ShowSpeechDialog(error);
                return false;
            }

            //Header matching, optional headers and the fill live in _PowerPath (PowerPath's report layout); undo, form
            //protection and Word error capture in VOScript.Starter._Word
            if (!_PowerPath.TransferReportToWord(Application, WindowTools.GetForegroundWindow(), document)) return false;

            document.MarkEvent("Report successfully transferred to AP System");
            _Premium.UpdateStage(Application, SpeechBox);

            if (DocumentStore.SaveAndFinalize(document)) SpeechBox.Close();
            Application.SetCommandDocument("Doc-PowerPath-MASTER");
            return true;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
