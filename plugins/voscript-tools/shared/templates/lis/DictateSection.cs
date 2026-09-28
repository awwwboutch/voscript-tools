#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{CoreNamespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Opens Report Builder for the case open in {{Vendor}}, at the spoken section. Trigger: dictate <$Sections>")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class DictateSection : CommandScript
    {
        public override void Execute()
        {
            DictateSection_Logic.Execute(Application, SpeechBox, DocumentStore, SpeechParams);
        }
    }

    public class DictateSection_Logic : ExtensionScript
    {
        /// <summary>
        /// Opens Report Builder on the case and moves to the section. Report Builder already open: just moves to the
        /// section. fromCaseNumber = the case was just opened by CaseNumber (or a scanner), whose number is already in the
        /// CaseNumber state property and whose speech parameters are not a section. Returns false when it stopped before
        /// Report Builder was ready.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore, SpeechParameters SpeechParams, bool fromCaseNumber = false)
        {
            // Author: {{Author}}
            // Date: {{Date}}

            if (Application.GetStateProperty("Edition") != "Premium")
            {
                StatusLog.WriteWarningEntry("DictateSection - only the Premium (Report Builder) edition is scaffolded for {{Vendor}}");
                return false;
            }

            string roleCategory = Application.GetStateProperty("RoleCategory");
            string section = (!fromCaseNumber && SpeechParams.Names.Contains("$Sections"))
                ? SpeechParams.TranslateToParams("$Sections").Translation
                : ((roleCategory == "SIGNOUT") ? "Diagnosis" : "Gross");

            if (SpeechBox.ActiveDocument != null) return FocusSection(SpeechBox, section);

            string caseNumber = fromCaseNumber ? Application.GetStateProperty("CaseNumber") : "";
//@if Word

            //Issued from the Word report, the case is in its title bar; otherwise take it from {{Vendor}} and open the report
            string wordCase = _{{System}}.WordCaseNumber(WindowTools.Instance.GetForegroundWindow());
            if (wordCase != "") caseNumber = wordCase;
            else
            {
                if (caseNumber == "") caseNumber = _{{System}}.LoadedCase(Application);
                if (caseNumber == "")
                {
                    _{{System}}.ShowWarning(Application, "No case is open in {{Vendor}}.  Please open the case and try again.");
                    return false;
                }
                IntPtr word = _{{System}}.OpenWordReport(Application, caseNumber);
                if (word == IntPtr.Zero) return false;
                caseNumber = _{{System}}.WordCaseNumber(word);
            }
//@else

            if (caseNumber == "") caseNumber = _{{System}}.LoadedCase(Application);
            if (caseNumber == "")
            {
                _{{System}}.ShowWarning(Application, "No case is open in {{Vendor}}.  Please open the case and try again.");
                return false;
            }
//@endif

            //Report Builder's template and the document store key off these, whichever window the case came from
            _{{System}}.SetCaseState(Application, caseNumber);

            return OpenReportBuilder(Application, SpeechBox, DocumentStore, section, roleCategory, _{{System}}.TemplateForCase(Application, caseNumber));
        }

        static bool OpenReportBuilder(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore, string section, string roleCategory, string template)
        {
            Application.SetCommandDocument(template);

            string documentID = "Document";
            if (Application.GetStateProperty("UserStatus") == "Test") documentID += "_Test";

            IDocument caseDocument = DocumentStore.LoadAndLock(documentID);
            if (!_Premium.GetWritelock(caseDocument, Application)) return false;
            _Premium.SetupDocument(caseDocument, DocumentStore, documentID, template, roleCategory);
            SpeechBox.StartDocument(caseDocument);

            return FocusSection(SpeechBox, section);
        }

        static bool FocusSection(ISpeechBoxState SpeechBox, string section)
        {
            IDocPart targetSection = SpeechBox.ActiveDocument.FindActivePart(section, DocPartTypes.Section);
            if (targetSection == null)
            {
                StatusLog.WriteErrorEntry("DictateSection - section '" + section + "' is not in the open document");
                return false;
            }
            if (targetSection.Visible == false)
            {
                targetSection.Visible = true;
                SpeechBox.RefreshDocumentChanges();
            }
            SpeechBox.SetInputFocus(targetSection);
            return true;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
