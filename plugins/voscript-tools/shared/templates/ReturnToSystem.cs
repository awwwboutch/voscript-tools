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
    [ExtensionCommandClass(HelpText = "Returns dictated text from Report Builder to {{Vendor}}. Standard trigger: [send report|return to {{Vendor}}]")]
    [ExtensionStringProperty("ReportTabName", DefaultValue = "", HelpText = "Report tab to focus before writing. Leave blank if the report is not behind a tab.")]
    [ExtensionStringProperty("ReportPanelName", DefaultValue = "Report Details*", HelpText = "Group that contains the report's section edit controls.")]
    [ExtensionStringProperty("CommandDocument", DefaultValue = "{{ReportTemplate}}", HelpText = "Command document to restore after the report is sent.")]
    [ExtensionIntProperty("MinimumSectionLength", DefaultValue = 5, HelpText = "Sections shorter than this are treated as empty and not written.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class {{ReturnToName}} : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will write the Report Builder text back into the matching {{Vendor}} report
            // fields, advance the document's stage, and release it.
            //
            // *****

            string titlePage = _{{System}}.RequireCaseWindow(Application);

            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();

            IUIAutomation automation = new CUIAutomation();
            IUIAutomationTreeWalker walker = automation.ControlViewWalker;

            try
            {
                IUIAutomationElement document = controller.FindWebPageDocument(titlePage);

                string reportTab = Property("ReportTabName", "");

                if (!string.IsNullOrWhiteSpace(reportTab))
                {
                    _Browser.FocusTabItemByName(document, reportTab);
                    WindowTools.Wait(200);
                }

                string reportPanelName = Property("ReportPanelName", "Report Details*");
                int minimumLength = Property("MinimumSectionLength", 5);

                if (SpeechBox.ActiveDocument != null)
                {
                    // Premium: one edit control per document section, matched by the section's
                    // own Label, so adding a section to the template needs no script change.
                    List<string> sections = SpeechBox.ActiveDocument.EnumerateParts(DocPartTypes.Section);

                    foreach (string section in sections)
                    {
                        IDocPart part = SpeechBox.ActiveDocument.FindActivePart(section, DocPartTypes.Section);

                        if (part.RenderText(RenderFlags.None).Length <= minimumLength)
                            continue;

                        // TODO: confirm the report panel anchor and that each field's accessible
                        // name matches the document part's Label exactly.
                        IUIAutomationElement reportPanelGroup = _Browser.FindElementOnPage(manager, reportPanelName, UIAControlType.Group);
                        IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, part.Label);

                        if (editControl == null)
                        {
                            StatusLog.WriteWarningEntry($"No {{Vendor}} field matched the \"{part.Label}\" section. That section was not sent.");
                            continue;
                        }

                        IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);

                        _Browser.SetElementText(editChild, part.RenderText(RenderFlags.None));
                        WindowTools.Wait(200);
                    }

                    SpeechBox.ActiveDocument.MarkEvent("Report successfully transferred to AP System");

                    string roleCategory = StateProperty("RoleCategory", "");

                    if (SpeechBox.ActiveDocument.Stage == "Final") SpeechBox.ActiveDocument.MarkNewStage("Amended");
                    else if (roleCategory == "SIGNOUT")            SpeechBox.ActiveDocument.MarkNewStage("Final");

                    if (DocumentStore.SaveAndRelease(SpeechBox.ActiveDocument)) SpeechBox.Close();
                }
                else if (TextEditor.ActiveDocument != null)
                {
                    // Classic: one section at a time, named by the document's WorkItem.
                    string sectionTitle = TextEditor.ActiveDocument.WorkItem;

                    IUIAutomationElement reportPanelGroup = _Browser.FindElementOnPage(manager, reportPanelName, UIAControlType.Group);
                    IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, sectionTitle);

                    if (editControl == null)
                        throw new ClientException($"No {{Vendor}} field matched the \"{sectionTitle}\" section. Nothing was sent.");

                    IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);

                    TextEditor.ExtractRtfContent();
                    string sectionText = TextEditor.ActiveDocument.RenderText(RenderFlags.Text);
                    _Browser.SetElementText(editChild, sectionText);

                    if (TextDocumentStore.SaveLocalAndRelease(TextEditor.ActiveDocument)) TextEditor.Close();
                }
                else
                {
                    StatusLog.WriteWarningEntry("No active Report Builder document. Nothing to send.");
                    return;
                }

                Application.SetCommandDocument(Property("CommandDocument", "{{ReportTemplate}}"));
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Warning rather than error: the dictation is still in Report Builder and the
                // pathologist can retry. Do not release the document on a failed transfer.
                StatusLog.WriteWarningEntry($"Error returning the report to {{Vendor}}: {ex.Message}");
            }
            finally
            {
                controller?.Dispose();
            }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
