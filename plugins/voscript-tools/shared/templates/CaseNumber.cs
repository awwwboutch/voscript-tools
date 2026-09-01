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
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Opens the spoken case from the {{Vendor}} worklist. Standard trigger: <{{CaseTypeList}}> [<Year> | ] [<Digit>|1-5]")]
    [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of the case number counter part.")]
    [ExtensionStringProperty("CaseTypeList", DefaultValue = "{{CaseTypeList}}", HelpText = "Name of the case type named list.")]
    [ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Determines if the case number is constructed with or without leading zeros.")]
    [ExtensionStringProperty("ReportTemplate", DefaultValue = "", HelpText = "Report Builder template. Leave blank to use the template mapped to the spoken case type.")]
    [ExtensionStringProperty("FallbackReportTemplate", DefaultValue = "{{ReportTemplate}}", HelpText = "Template used when the spoken case type has no template mapped to it.")]
    [ExtensionIntProperty("SearchTimeout", DefaultValue = 5000, HelpText = "Milliseconds to wait for the case to appear in the worklist.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseNumber : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will search for the spoken case in the {{Vendor}} worklist, open it, and
            // optionally start Report Builder on it.
            //
            // *****

            string caseNumber = _CaseNumber.FormatParameterizedSpeech(
                SpeechParams,
                Property("CounterLength", 5),
                Property("CaseTypeList", "{{CaseTypeList}}"),
                Property("LeadingZeros", true));

            caseNumber = NormalizeCaseNumber(caseNumber);

            // Set this first. NavigateSlides, DictateSection and everything downstream read it,
            // and they must not see a stale case number while the browser is still loading.
            Application.SetStateProperty("CaseNumber", caseNumber);

            string titlePage = _{{System}}.FindCurrentTitlePage(Application);

            if (string.IsNullOrEmpty(titlePage))
                throw new ClientException("No {{Vendor}} window was found. Open {{Vendor}} first.");

            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                // TODO: verify every element name below against the live worklist with
                // FlaUInspect or _Browser.DumpTree(controller.FindWebPageDocument(titlePage)).

                // Point the worklist filter at the accession column before searching, so the
                // search text is not applied to whatever column the user left selected.
                IUIAutomationElement filterAnchor = _Browser.FindElementOnPage(manager, "Cases", UIAControlType.Text);
                IUIAutomationElement filterPulldown = _Browser.GetNextSiblingElement(filterAnchor);
                _Browser.SetElementText(filterPulldown, "Accession #");
                WindowTools.Wait(150);

                IUIAutomationElement searchField = _Browser.FindElementOnPage(manager, "Search cases", UIAControlType.Edit);
                _Browser.SetElementText(searchField, caseNumber);

                // Wait on the result rather than sleeping: worklist filtering is a round trip.
                if (!_Browser.WaitForElement(manager, caseNumber, UIAControlType.Hyperlink, Property("SearchTimeout", 5000)))
                    throw new ClientException($"Case {caseNumber} did not appear in the {{Vendor}} worklist.");

                IUIAutomationElement caseLink = _Browser.FindElementOnPage(manager, caseNumber, UIAControlType.Hyperlink);
                controller.Click(caseLink);
            }
            catch (ClientException)
            {
                // Already carries a message the user can act on. Do not fall through to Report
                // Builder: opening a document for a case that never loaded is worse than failing.
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
                throw new ClientException($"Could not open case {caseNumber} in {{Vendor}}. {ex.Message}");
            }

            OpenReportBuilder(caseNumber);

        } // close Execute

        /// <summary>
        /// Starts the Report Builder document for the case and drops the cursor in Diagnosis.
        /// Only runs when the case actually opened and the palette asked for it.
        /// </summary>
        private void OpenReportBuilder(string caseNumber)
        {
            // Check the cheap flag before taking a write lock. GetWritelock has side effects and
            // should not run on a pathway that is about to be abandoned.
            if (StateProperty("OpenRB", "") != "True")
                return;

            IDocument caseDocument = DocumentStore.LoadAndLock("Document");

            if (!_Premium.GetWritelock(caseDocument, Application))
            {
                StatusLog.WriteWarningEntry($"Could not get a write lock on the document for {caseNumber}. Report Builder was not opened.");
                return;
            }

            _Premium.SetupDocument(caseDocument, DocumentStore, "Document", ResolveReportTemplate(), "SIGNOUT");
            SpeechBox.StartDocument(caseDocument);

            IDocPart diagnosisSection = caseDocument.FindActivePart("Diagnosis", DocPartTypes.Section);

            if (diagnosisSection == null)
            {
                StatusLog.WriteWarningEntry("No active Diagnosis section was found in the document.");
                SpeechBox.SpeechInputAnchor.Enable();
                return;
            }

            if (diagnosisSection.Visible == false)
            {
                diagnosisSection.Visible = true;
                SpeechBox.RefreshDocumentChanges();
            }

            SpeechBox.SetInputFocus(diagnosisSection);
            SpeechBox.SpeechInputAnchor.Enable();
        }

        /// <summary>
        /// Report Builder template for this case. An explicit ReportTemplate property wins;
        /// otherwise the template mapped to the spoken case type is used, so a Cytology case
        /// does not get the Surgical template.
        /// </summary>
        private string ResolveReportTemplate()
        {
            string configured = Property("ReportTemplate", "");

            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            try
            {
                string mapped = SpeechParams.TranslateToParams(Property("CaseTypeList", "{{CaseTypeList}}")).Target;

                if (!string.IsNullOrWhiteSpace(mapped))
                    return mapped;
            }
            catch (Exception ex)
            {
                StatusLog.WriteWarningEntry($"Could not map the spoken case type to a template: {ex.Message}");
            }

            return Property("FallbackReportTemplate", "{{ReportTemplate}}");
        }

        /// <summary>
        /// Guards against a double dash in the built accession number. That only happens when an
        /// entry in the case type named list carries its own trailing dash, because
        /// _CaseNumber.FormatParameterizedSpeech already joins with one. Warn rather than
        /// silently papering over it, so the named list actually gets fixed.
        /// </summary>
        private string NormalizeCaseNumber(string caseNumber)
        {
            if (string.IsNullOrWhiteSpace(caseNumber))
                throw new ClientException("No accession number was built from the spoken command.");

            if (caseNumber.Contains("--"))
            {
                StatusLog.WriteWarningEntry(
                    $"Built accession number \"{caseNumber}\" contains a double dash. " +
                    "An entry in the case type named list most likely ends with a dash - remove it there.");

                caseNumber = caseNumber.Replace("--", "-");
            }

            return caseNumber;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
