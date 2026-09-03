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
    [ExtensionCommandClass(HelpText = "Reads the case number off the open case and initializes Report Builder for it. Trigger: the word dictate followed by the shared Sections named list")]
    [ExtensionStringProperty("AccessionLabel", DefaultValue = "Accession", HelpText = "On-screen label sitting next to the accession number.")]
    [ExtensionStringProperty("ReportTabName", DefaultValue = "", HelpText = "Report tab to focus in Classic. Leave blank if the report is not behind a tab.")]
    [ExtensionStringProperty("ReportPanelName", DefaultValue = "Report Details*", HelpText = "Group that contains the report's section edit controls.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class DictateSection : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will read the case number from the open {{Vendor}} case and launch Report
            // Builder on it. In Premium the document opens in SpeechBox; in Classic the cursor
            // is placed in the matching field in the IMS report itself.
            //
            // *****

            string titlePage = _{{System}}.RequireCaseWindow(Application);

            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();

            try
            {
                IUIAutomationElement document = controller.FindWebPageDocument(titlePage);

                IUIAutomation automation = new CUIAutomation();
                IUIAutomationTreeWalker walker = automation.ControlViewWalker;

                // TODO: confirm how the accession number is exposed. The common shape is a
                // label element with the value as its next sibling. If {{Vendor}} puts it in
                // the window title instead, parse it from titlePage rather than the DOM.
                IUIAutomationElement accessionLabel = _Browser.FindElementOnPage(manager, Property("AccessionLabel", "Accession"), UIAControlType.Text);

                if (accessionLabel == null)
                    throw new ClientException("Could not read the accession number from the open {{Vendor}} case.");

                IUIAutomationElement accessionValue = _Browser.GetNextSiblingElement(accessionLabel);
                string caseNumber = accessionValue.CurrentName;

                Application.SetStateProperty("CaseNumber", caseNumber);

                string edition = StateProperty("Edition", "");
                string openRB = StateProperty("OpenRB", "False");

                // TODO: confirm how the case type is derived from the accession number. Two
                // leading characters is the common shape but not universal.
                string caseType = caseNumber.Length >= 2 ? caseNumber.Substring(0, 2) : caseNumber;
                TranslationParams caseTypeParams = Application.FindTranslationParams("{{CaseTypeList}}", caseType);

                IDocument caseDocument = DocumentStore.LoadAndLock("Document");

                if (edition == "Premium" && openRB == "True" && _Premium.GetWritelock(caseDocument, Application))
                {
                    _Premium.SetupDocument(caseDocument, DocumentStore, "Document", caseTypeParams.Target, "SIGNOUT");
                    SpeechBox.StartDocument(caseDocument);

                    IDocPart diagnosisSection = caseDocument.FindActivePart("Diagnosis", DocPartTypes.Section);

                    if (diagnosisSection != null)
                    {
                        if (diagnosisSection.Visible == false)
                        {
                            diagnosisSection.Visible = true;
                            SpeechBox.RefreshDocumentChanges();
                        }

                        SpeechBox.SetInputFocus(diagnosisSection);
                    }
                    else
                    {
                        StatusLog.WriteWarningEntry("No active Diagnosis section was found in the document.");
                    }

                    SpeechBox.SpeechInputAnchor.Enable();
                }
                else if (edition == "Classic")
                {
                    // TODO: map the spoken section names to this system's field labels. The
                    // spoken side comes from the shared $Sections list; the right-hand side is
                    // whatever {{Vendor}} calls the field.
                    string targetSectionName = SpeechParams.TranslateSingle(SpeechParams.Names[0]);

                    switch (targetSectionName)
                    {
                        case "Diagnosis": targetSectionName = "Final Impression";       break;
                        case "Micro":     targetSectionName = "Microscopic Description"; break;
                        case "Gross":     targetSectionName = "Gross Description";       break;
                        case "Comment":   targetSectionName = "Comments";                break;
                        default:
                            StatusLog.WriteWarningEntry($"Section \"{targetSectionName}\" has no field mapping in DictateSection.");
                            return;
                    }

                    // Recover an existing document from autosave, or create a new one.
                    if (TextEditor == null && openRB == "True")
                    {
                        Application.SetStateProperty("TextEditorFontName", "Arial");
                        Application.SetStateProperty("TextEditorFontSize", "12");

                        string documentID = string.IsNullOrEmpty(targetSectionName) ? "Document" : targetSectionName;
                        ITextDocument startDocument = TextDocumentStore.Load(documentID);
                        TextEditor.StartDocument(startDocument);
                        TextEditor.SpeechInputAnchor.Enable();
                    }
                    else
                    {
                        string reportTab = Property("ReportTabName", "");

                        if (!string.IsNullOrWhiteSpace(reportTab))
                        {
                            _Browser.FocusTabItemByName(document, reportTab);
                            WindowTools.Wait(200);
                        }

                        IUIAutomationElement reportPanelGroup = _Browser.FindElementOnPage(manager, Property("ReportPanelName", "Report Details*"), UIAControlType.Group);
                        IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, targetSectionName);
                        IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);

                        controller.Click(editChild);
                        WindowTools.Wait(200);

                        InputSimulator sim = new InputSimulator();
                        sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.HOME);
                    }
                }
                else
                {
                    StatusLog.WriteWarningEntry($"Edition is \"{edition}\" and OpenRB is \"{openRB}\". Nothing to do.");
                }
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
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
