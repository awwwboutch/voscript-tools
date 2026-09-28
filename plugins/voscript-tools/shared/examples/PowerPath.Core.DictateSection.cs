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
  [ExtensionCommandClass(HelpText = "Opens speechbox and navigates to the spoken section.  If role is SIGNOUT, wil also open Word report.  Standard Triggers:  Dictate <$Sections>")]
  [ExtensionBoolProperty("GrossOpensWord", DefaultValue = false, LabelText = "Open Word for Gross Role?", HelpText = "Determines whether or not Word should be open for gross role.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class DictateSection : CommandScript
    {
        public override void Execute()
        {
            DictatePowerPath_Logic.Execute(Application, SpeechBox, DocumentStore, TextDocumentStore, TextEditor, SpeechParams);
        }
    }

    public class DictatePowerPath_Logic : ExtensionScript
    {
        /// <summary>
        /// Opens Report Builder (Premium) or the Word section (Classic) for the spoken section. Issued from the Word
        /// report, the case number comes off Word's title bar; issued from PowerPath Client (or anywhere else), it comes
        /// from the open case and the Word report is opened first unless it already is. scanned = the case number is
        /// already in the CaseNumber state property (CaseNumber command, ScanContainer). Returns false when it stopped
        /// before the report was ready.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox, IDocumentStore DocumentStore, ITextDocumentStore TextDocumentStore, ITextEditorState TextEditor, SpeechParameters SpeechParams, bool scanned = false)
        {
            string edition = Application.GetStateProperty("Edition");
            string roleCategory = Application.GetStateProperty("RoleCategory");

            string section = (SpeechParams.Names.Count == 0 || scanned)
                ? ((roleCategory == "SIGNOUT") ? "Diagnosis" : "Gross")
                : SpeechParams.TranslateToParams("$Sections").Translation;

            //Report Builder already open: just move to the section, PowerPath and Word don't need touching
            if (edition == "Premium" && SpeechBox.ActiveDocument != null)
                return FocusSection(SpeechBox, section);

            string caseNumber = OpenWordReport(Application, scanned);
            if (string.IsNullOrEmpty(caseNumber)) return false;

            //GetOutlineTemplate and the document store key off these, so set them whichever window the case came from
            Application.SetStateProperty("CaseNumber", caseNumber);
            Match caseMatch = Regex.Match(caseNumber, "^" + _PowerPath.caseNumberPattern + "$");
            if (caseMatch.Success) Application.SetStateProperty("CaseType", caseMatch.Groups[2].Value);

            //Looking up the target template associated with case prefix in the CaseType global named list
            string targetTemplateName = _Premium.GetOutlineTemplate(Application);
            if (string.IsNullOrEmpty(targetTemplateName)) return false;

            string documentID = "Document";
            if (edition == "Premium")
            {
                if (section.ToLower() == "addendum")
                {
                    string addendumNumber = _Premium.WhichAddendum(DocumentStore, Application, caseNumber);
                    documentID = "Addendum_" + addendumNumber;
                    targetTemplateName = "Doc-PowerPath-Addendum";
                }
                return OpenPremium(Application, DocumentStore, SpeechBox, section, roleCategory, documentID, targetTemplateName);
            }

            JumpToSection(SpeechBox, section, targetTemplateName);
            return true;
        }

        /// <summary>
        /// Gets the Word report for the current case in front and returns its case number, or "" if it couldn't.
        /// </summary>
        static string OpenWordReport(IApplicationControl Application, bool scanned)
        {
            WindowTools WindowTools = WindowTools.Instance;

            //Issued from the Word report: the case number is in the title bar, nothing else to do
            string foregroundCase = WordCaseNumber(WindowTools.GetForegroundWindow());
            if (foregroundCase != "") return foregroundCase;

            string caseNumber = scanned ? Application.GetStateProperty("CaseNumber") : _PowerPath.GetCaseNumberPowerPath(Application);
            if (string.IsNullOrEmpty(caseNumber)) return "";

            //Word report already open behind PowerPath: bring it forward rather than pressing Edit Results again
            IntPtr openWord = WindowTools.FindWindow(_PowerPath.WordTitlePattern);
            if (openWord != IntPtr.Zero)
            {
                string openCase = WordCaseNumber(openWord);
                if (!openCase.Equals(caseNumber, StringComparison.OrdinalIgnoreCase))
                {
                    ShowWarning(Application, "VoiceOver PRO - PowerPath",
                        string.Format("The Word report for case {0} is still open.\n\nPlease save or close it before dictating case {1}.", openCase, caseNumber));
                    return "";
                }
                WindowTools.EnsureForegroundWindow(openWord);
                return openCase;
            }

            if (scanned) _PowerPath.FocusPowerPath();
            _PowerPath.FocusResults();
            _PowerPath.PressEdit();     //Edit Results button; avoids a weird error prompt from PPath that !u seems to trigger
            return WaitForWordReport(Application);
        }

        /// <summary>
        /// After Edit Results: answers PowerPath's prompts as they appear and returns as soon as the Word report is open
        /// and answering automation, instead of sitting through fixed waits for prompts that usually never show.
        /// </summary>
        static string WaitForWordReport(IApplicationControl Application)
        {
            WindowTools WindowTools = WindowTools.Instance;
            int confirmsAnswered = 0;
            IntPtr word = IntPtr.Zero;
            DateTime deadline = DateTime.Now.AddSeconds(15);

            while (DateTime.Now < deadline)
            {
                if (WindowTools.FindWindow("PowerPath Client Error") != IntPtr.Zero)
                {
                    StatusLog.WriteErrorEntry("DictateSection - PowerPath Client Error displayed after Edit Results");
                    return "";
                }
                if (WindowTools.IsForegroundWindow("^Information$"))
                {
                    HandleSignedOutCase(Application);   //Information prompt displays if case is already signed out
                    return "";
                }
                if (confirmsAnswered < 2 && WindowTools.IsForegroundWindow("^Confirm"))
                {
                    Automation.Send("y");
                    confirmsAnswered++;
                    WindowTools.Wait(300);
                    continue;
                }
                word = WindowTools.FindWindow(_PowerPath.WordTitlePattern);
                if (word != IntPtr.Zero) break;
                WindowTools.Wait(100);
            }

            if (word == IntPtr.Zero)
            {
                ShowWarning(Application, "VoiceOver PRO - PowerPath", "Word Report did not open successfully.  Please ensure Word Report is open.");
                return "";
            }

            //Word's window shows up before Word is usable: wait for it to answer automation instead of a flat second
            _Word.WaitUntilResponds(word);
            WindowTools.EnsureForegroundWindow(word);
            return WordCaseNumber(word);
        }

        /// <summary>
        /// Case number from a PowerPath Word report window title ("Case 'S-26-00026' - ..."), or "" if it isn't one.
        /// </summary>
        static string WordCaseNumber(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            Match match = Regex.Match(WindowTools.Instance.GetWindowText(hWnd), _PowerPath.WordTitlePattern);
            return match.Success ? match.Groups[1].Value : "";
        }

        static void HandleSignedOutCase(IApplicationControl Application)
        {
            WindowTools WindowTools = WindowTools.Instance;
            Automation.Send("{ENTER}");

            SpeechDialogSettings settings = new SpeechDialogSettings(
                SpeechDialogSettings.IconTypes.Warning,
                "Case Not Editable",
                "This case has been signed out and is not editable.\nChoose one of the below options to proceed:");
            int addendum = settings.AddChoice("Addendum", "Create an addendum on the current case.");
            int amendment = settings.AddChoice("Amendment", "Make an amendment to the current case.");
            //int correction = settings.AddChoice("Correction", "Make a correction to the current case.");
            int cancel = settings.AddChoice("Cancel", "Make no changes to the current case.");
            int result = Application.ShowSpeechDialog(settings);

            if (result == amendment || result == addendum)// || result == correction)
            {
                Automation.Send("^s", 100);                 //opens Case Status window from PowerPath
                WindowTools.WaitForWindow("^Case Status");
                Automation.Send("!p{TAB}", 150);            //Progress Status checkbox and place focus in New Step field
                if (result == amendment) Automation.Send("am", 50);
                else if (result == addendum) Automation.Send("ad", 50);
                Automation.Send("{TAB 2}{ENTER}", 50);      //click Ok to close dialog and set the next status
            }
        }

        /// <summary>
        /// Opens Report Builder on the case's document and moves to the section
        /// </summary>
        private static bool OpenPremium(IApplicationControl Application, IDocumentStore DocumentStore, ISpeechBoxState SpeechBox, string section, string roleCategory, string documentID, string targetTemplateName)
        {
            WindowTools WindowTools = WindowTools.Instance;
            if (SpeechBox.ActiveDocument == null)
            {
                Application.SetCommandDocument(targetTemplateName);
                if (Application.GetStateProperty("UserStatus") == "Test" || WindowTools.GetWindowText(WindowTools.FindWindow("^PowerPath")).Contains("Test System")) documentID += "_Test";

                IDocument startDocument = DocumentStore.LoadAndLock(documentID);
                if (SpeechBox.ActiveDocument == null)
                {
                    if (!_Premium.GetWritelock(startDocument, Application)) return false;
                    _Premium.SetupDocument(startDocument, DocumentStore, documentID, targetTemplateName, roleCategory);

                    SpeechBox.StartDocument(startDocument);

                    string patientName = Application.GetStateProperty("PatientName");
                    IDocField patientNameField = SpeechBox.ActiveDocument.FindField("PatientName");
                    if (patientNameField != null && patientName != null && patientName != string.Empty)
                        patientNameField.Value = patientName;
                }
            }
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

        /// <summary>
        /// Jumps to the specified Word section for PRO Classic implemention in Word.  This assumes that blank sections will have a set of brackets underneath the header like [X]
        /// </summary>
        private static void JumpToSection(ISpeechBoxState SpeechBox, string section, string targetTemplateName)
        {
            WindowTools WindowTools = WindowTools.Instance;
            MSWordTools MSWordTools = MSWordTools.Instance;
            IDocument outline = SpeechBox.CreateDocument(targetTemplateName);
            string sectionLabel = outline.FindActivePart(section, DocPartTypes.Section).Label;

            Automation.ClipSet("");

            MSWordTools.SetWordContext(WindowTools.GetForegroundWindow());
            MSWordTools.SelectionGoto(MSWordGoToItem.wdGoToLine, MSWordGoToDirection.wdGoToFirst);

            MSWordTools.SelectionHomeKey(MSWordUnits.wdStory);

            bool foundSectionBrackets = MSWordTools.FindSectionBrackets(sectionLabel, 4, 2);

            if (foundSectionBrackets)
            {
                //Example for setting font characteristics for a section
                /*
                if (sectionLabel == "FINAL DIAGNOSIS:")
                {
                    MSWordTools.SelectionFontSize(fontSizeDiagnosis);
                    MSWordTools.SelectionFontBold(1);
                }*/
            }

            MSWordTools.ReleaseContext();
        }

        static void ShowWarning(IApplicationControl Application, string title, string message)
        {
            SpeechDialogSettings dialog = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, title, message);
            dialog.AddChoice("OK", "Press OK to close this dialog");
            Application.ShowSpeechDialog(dialog);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE

