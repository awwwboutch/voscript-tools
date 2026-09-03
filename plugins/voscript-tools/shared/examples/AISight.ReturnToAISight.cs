// SOURCE: VOScript.Starter.AISight.ReturnToAISight@5P-3034-3.xml
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
#endregion USING NAMESPACES

namespace VOScript.Starter.AISight
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class ReturnToAISight : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 12/4/2025
            // Use:
            // This will return text from Report Builder back to AISight
            //
            // *****
            
            string titlePage = _AISight.FindCurrentTitlePage(Application);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();
            IUIAutomationElement document = controller.FindWebPageDocument(titlePage);
            
            IUIAutomation automation = new CUIAutomation();
            IUIAutomationTreeWalker walker = automation.ControlViewWalker;
            
            try
            {
                _Browser.FocusTabItemByName(document, "Accession Report");
                System.Threading.Thread.Sleep(200);
                
                if (SpeechBox.ActiveDocument != null)
                {
                    List<string> sections = SpeechBox.ActiveDocument.EnumerateParts(DocPartTypes.Section);
                    foreach (string section in sections)
                    {
                        IDocPart part = SpeechBox.ActiveDocument.FindActivePart(section, DocPartTypes.Section);
                        
                        //Find the top of the report panel for use in finding the children edit controls
                        IUIAutomationElement reportPanelGroup = _Browser.FindElementOnPage(manager, "Report Details*", UIAControlType.Group);
                        //IUIAutomationElement reportPanelGroup = _Browser.GetNextSiblingElement(reportPanelHeader);
                        
                        //Find the edit control using the label value of the section in Report Builder
                        IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, part.Label);
                        IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);
                        
                        if (part.RenderText(RenderFlags.None).Length > 5)
                        {
                            _Browser.SetElementText(editChild, part.RenderText(RenderFlags.None));
                            WindowTools.Wait(200);
                        }
                    }
                    
                    SpeechBox.ActiveDocument.MarkEvent("Report successfully transferred to AP System");
    
                    string roleCategory = StateProperty("RoleCategory", "");
                    if (SpeechBox.ActiveDocument.Stage == "Final") SpeechBox.ActiveDocument.MarkNewStage("Amended");
                    else if (roleCategory == "SIGNOUT") SpeechBox.ActiveDocument.MarkNewStage("Final");
        
                    if (DocumentStore.SaveAndRelease(SpeechBox.ActiveDocument)) SpeechBox.Close();
                }
                else if (TextEditor.ActiveDocument != null)
                {
                    string sectionTitle = TextEditor.ActiveDocument.WorkItem;
                    
                     //Find the top of the report panel for use in finding the children edit controls
                    IUIAutomationElement reportPanelHeader = _Browser.FindElementOnPage(manager, "Report Details*", UIAControlType.Group);
                    IUIAutomationElement reportPanelGroup = _Browser.GetNextSiblingElement(reportPanelHeader);
                    
                    //Find the edit control using the label value of the section in Report Builder
                    IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, sectionTitle);
                    IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);
                    
                    TextEditor.ExtractRtfContent();
                    string sectionText = TextEditor.ActiveDocument.RenderText(RenderFlags.Text);
                    _Browser.SetElementText(editChild, sectionText);
                    
                    if (TextDocumentStore.SaveLocalAndRelease(TextEditor.ActiveDocument)) TextEditor.Close();
                    
                }
    
                Application.SetCommandDocument("Doc-AISight-Surgical");
            }
            catch (Exception ex)
            {
                StatusLog.WriteWarningEntry($"Error controlling AISight:  {ex.Message}");
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


