// SOURCE: VOScript.Starter.AISight.DictateSection@6P-3122-4.xml
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
using WindowsInput.Native;
#endregion USING NAMESPACES

namespace VOScript.Starter.AISight
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class DictateSection : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 12/4/2025
            // Use:
            // This will read the case number from AISight and launch Report Builder
            //
            // *****
            
            //First group here will find and click the completed cases button
            string titlePage = _AISight.FindCurrentTitlePage(Application);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();
            IUIAutomationElement document = controller.FindWebPageDocument(titlePage);
            
            string caseNumber = "";

            try
            {
                IUIAutomation automation = new CUIAutomation();
                IUIAutomationTreeWalker walker = automation.ControlViewWalker;
                
                //Finding accession number on page by finding the Accession label, getting the previous sibling, and reading its name
                IUIAutomationElement accessionLabel = _Browser.FindElementOnPage(manager, "Accession", UIAControlType.Text);
                IUIAutomationElement accessionValue = _Browser.GetNextSiblingElement(accessionLabel);
                caseNumber = accessionValue.CurrentName;
                Application.SetStateProperty("CaseNumber", caseNumber);      
                //Report Builder Logic
                
                IDocument caseDocument = DocumentStore.LoadAndLock("Document");
            
                string edition = StateProperty("Edition", "");
                string caseType = caseNumber.Substring(0,2);
                TranslationParams caseTypeParams = Application.FindTranslationParams("AISightCaseType", caseType);
                
                string openRB = StateProperty("OpenRB", "False");
                
                if (edition == "Premium" && openRB == "True" && _Premium.GetWritelock(caseDocument, Application) )
                {
                    _Premium.SetupDocument(caseDocument, DocumentStore, "Document", caseTypeParams.Target, "SIGNOUT");
                    SpeechBox.StartDocument(caseDocument);
                    IDocPart diagnosisSection = caseDocument.FindActivePart("Diagnosis", DocPartTypes.Section);
                    if (diagnosisSection.Visible == false)
                    {
                        diagnosisSection.Visible = true;
                        SpeechBox.RefreshDocumentChanges();
                    }
                    SpeechBox.SetInputFocus(diagnosisSection);
                    SpeechBox.SpeechInputAnchor.Enable();
                }
                else if (edition == "Classic")
                {
                    string targetSectionName = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
                    switch (targetSectionName)
                    {
                        case "Diagnosis":
                            targetSectionName = "Final Impression";
                            break;
                        case "Micro":
                            targetSectionName = "Microscopic Description";
                            break;
                        case "Gross":
                            targetSectionName = "Gross Description";
                            break;
                        case "Comment":
                            targetSectionName = "Comments";
                            break;
                        default:
                            return;
                    }
                    // recover an existing document from autosave or create a new document
                    if (TextEditor == null && Application.GetStateProperty("OpenRB") == "True")
                    {
                        // set font characteristics before opening the TextEditor window
                        Application.SetStateProperty("TextEditorFontName", "Arial");
                        Application.SetStateProperty("TextEditorFontSize", "12");
        
                        string documentID = string.IsNullOrEmpty(targetSectionName) ? "Document" : targetSectionName;
                        ITextDocument startDocument = TextDocumentStore.Load(documentID);
                        TextEditor.StartDocument(startDocument);
                        TextEditor.SpeechInputAnchor.Enable();
                    }
                    else
                    {
                        if (!_Browser.IsElementVisible(manager, "Accession Report"))
                        {
                            IUIAutomationElement toggleButton = _Browser.FindElementOnPage(manager, "Toggle Slide Info Panel", UIAControlType.Button);
                            WindowTools.Wait(500);
                            controller.Click(toggleButton);
                        }
                        _Browser.FocusTabItemByName(document, "Accession Report");
                        System.Threading.Thread.Sleep(200);
                        
                         //Find the top of the report panel for use in finding the children edit controls
                        IUIAutomationElement reportPanelGroup = _Browser.FindElementOnPage(manager, "Report Details*", UIAControlType.Group);
                        
                        //Find the edit control using the label value of the section in Report Builder
                        IUIAutomationElement editControl = _Browser.GetChildByName(reportPanelGroup, targetSectionName);
                        IUIAutomationElement editChild = walker.GetFirstChildElement(editControl);  
                    
                        controller.Click(editChild);
                        WindowTools.Wait(200);
                        
                        InputSimulator sim = new InputSimulator();
                        sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.HOME);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling AISight Workflow: {ex.Message}", ex);
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



