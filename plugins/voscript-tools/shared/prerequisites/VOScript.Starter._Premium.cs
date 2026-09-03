// PREREQUISITE: VOScript.Starter._Premium
// Source revision 1P-2154-3, exported 2026-09-01 from demo\sales.
// Tier: reporting
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using System.Text.RegularExpressions;
using VOScript.Starter;
#endregion USING NAMESPACES

namespace VOScript.Starter
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _Premium : ExtensionScript
    {
        /// <summary>
        /// This function wlil check to see if the current user can get writelock on the current case.  Returns false if not.
        /// </summary>
        /// <param name="startDocument"></param>
        /// <param name="Application"></param>
        /// <returns></returns>
        public static bool GetWritelock(IDocument startDocument, IApplicationControl Application)
        {
            if (!startDocument.HoldingLock) // document server refused to grant writelock
            {
                string message = (startDocument.WorkItem == "Document") ?
                string.Format("Cannot open case {0}\nLocked by user {1} on {2}", startDocument.CaseNumber, startDocument.LastUser, startDocument.LastWorkstation) :
                string.Format("Cannot open case {0}/{1}\nLocked by user {2} on {3}", startDocument.CaseNumber, startDocument.WorkItem, startDocument.LastUser, startDocument.LastWorkstation);

                SpeechDialogSettings dialogSettings = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "Cannot Open Case", message);
                int ok = dialogSettings.AddChoice("OK", "Say OK to continue");
                Application.ShowSpeechDialog(dialogSettings);
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// This function will check to see if the document is new and, if it is not, it will initialize the document with the specified document outline.  It will return "New" if the document did not already exist or "Old" if it already existed and was pulled from the database.
        /// </summary>
        /// <param name="startDocument"></param>
        /// <param name="DocumentStore"></param>
        /// <param name="documentID"></param>
        /// <param name="targetTemplateName"></param>
        /// <param name="roleCategory"></param>
        /// <returns></returns>
        public static string SetupDocument(IDocument startDocument, IDocumentStore DocumentStore, string documentID, string targetTemplateName, string roleCategory)
        {
            if (startDocument.IsNew)
            {
                if (startDocument.OfflineStart && roleCategory == "SIGNOUT")
                {
                    DocumentStore.Abandon(startDocument);
                    startDocument = (documentID == "Addendum") ?
                        DocumentStore.LoadAndLock("Addendum_Offline_" + DateTime.Today.ToString("MM-dd-yyyy_HH:mm")) :
                        DocumentStore.LoadAndLock("Document_Offline_" + DateTime.Today.ToString("MM-dd-yyyy_HH:mm"));
                }
                startDocument.Initialize(targetTemplateName);
                return "New";
            }
            return "Old";
        }
        
        /// <summary>
        /// This will return the target template name associated with the current case prefix
        /// </summary>
        /// <param name="Application"></param>
        /// <returns></returns>
        public static string GetOutlineTemplate(IApplicationControl Application)
        {
            string caseType = Application.GetStateProperty("CaseType");
            if (caseType == string.Empty) {StatusLog.WriteErrorEntry("CaseType not captured.  Unable to launch ReportBuilder"); return null;}
            TranslationParams caseTypeParams = (Application.FindTranslationParams("CaseType", caseType));
            return caseTypeParams.Target;
        }
        
        /// <summary>
        /// This function checks to see if an addendum already exists.  If it does, it will ask the user if they want to open a previous addendum OR start a new one.  Returns the appropriate documentID based on the answer to the prompt.
        /// </summary>
        /// <param name="DocumentStore"></param>
        /// <param name="Application"></param>
        /// <param name="caseNumber"></param>
        /// <returns></returns>
        public static string WhichAddendum(IDocumentStore DocumentStore, IApplicationControl Application, string caseNumber)
        {   
            List<string> documents = DocumentStore.GetDocuments(caseNumber);
            
            if (!documents.Contains("Addendum 1"))
                return "Addendum 1";
            else if (documents.Contains("Addendum 1"))
            {
                SpeechDialogSettings prompt = new SpeechDialogSettings(
                    SpeechDialogSettings.IconTypes.Question,
                    "Which Addendum?",
                    "Please select the addendum that you'd like to re-open, or select \"new\" to start a new one");
                int addendumCount = 0;
                foreach (string document in documents)
                {
                    if (!document.Contains("Addendum")) continue;
                    prompt.AddChoice(document, "");
                    addendumCount++;
                }
                int newAddendum = prompt.AddChoice("New", "Create a new addendum");
                int result = Application.ShowSpeechDialog(prompt);
                if (result == newAddendum)
                    return "Addendum " + (addendumCount+1).ToString();
                else
                    return documents[result+1];
            }
            return null;
        }
        
        public static void UpdateStage(IApplicationControl Application, ISpeechBoxState SpeechBox)
        {
            string roleCategory = Application.GetStateProperty("RoleCategory");
            if (SpeechBox.ActiveDocument.Stage == "Final" || SpeechBox.ActiveDocument.Stage == "Amended") SpeechBox.ActiveDocument.MarkNewStage("Amended");
            else if (roleCategory == "SIGNOUT" && Application.GetStateProperty("UserType") == "Resident") SpeechBox.ActiveDocument.MarkNewStage("Resident Review Complete");
            else if (roleCategory == "SIGNOUT") SpeechBox.ActiveDocument.MarkNewStage("Final Diagnosis Complete");
            else if (Application.GetStateProperty("RoleCategory") == "GROSS") SpeechBox.ActiveDocument.MarkNewStage("Gross Complete");
        }
        
        public static void BuildDxFromGross(ISpeechBoxState SpeechBox)
        {
            string dxText = SpeechBox.ActiveDocument.FindActivePart("Diagnosis", DocPartTypes.Section).RenderText(RenderFlags.None);
            if (!dxText.Trim().Any(c => Char.IsLetterOrDigit(c)) && dxText.Length < 20)
            {
                IDocList grossList = SpeechBox.ActiveDocument.FindActivePart("GrossList", DocPartTypes.List) as IDocList;
                
                IDocPart diagnosisSection = SpeechBox.ActiveDocument.FindActivePart("Diagnosis", DocPartTypes.Section);
                diagnosisSection.Visible = true;
                IDocList diagnosisList = SpeechBox.ActiveDocument.FindActivePart("DiagnosisList", DocPartTypes.List) as IDocList;
                diagnosisList.Items.Clear();
                for (int index = 0; index < grossList.Items.Count; index++)
                {
                    IDocListItem copySpecimen = grossList.Items[index];
                    HashSet<string> tags = copySpecimen.TagSet;
                    bool build = tags.Contains("AutoBuild");
                    string grossTemplateName = copySpecimen.PartKey.Substring(copySpecimen.PartKey.LastIndexOf(".")+1);
                    
                    string dxTemplateName = (GetTemplateName(tags) == null) ? grossTemplateName : GetTemplateName(tags);
                    StatusLog.WriteErrorEntry(dxTemplateName);
                    
                    IDocListItem diagnosisPart = (build) ? SpeechBox.FindLibraryPart("DiagnosisList." + dxTemplateName) as IDocListItem : SpeechBox.FindLibraryPart("DiagnosisPart") as IDocListItem;
                    diagnosisList.Items.Add(diagnosisPart);
                    SpeechBox.RefreshDocumentChanges();
                    
                    string label = (copySpecimen.FindField("Site") == null) ? "" : copySpecimen.FindField("Site").Value;
    
                    if (label != "")
                    {
                        diagnosisPart.FindField("Site").Value = label.First().ToString().ToUpper() + label.Substring(1);
                    }
    
                }
                SpeechBox.RefreshDocumentChanges();
                SpeechBox.SetInputFocus(diagnosisList);
                SpeechBox.SetInputFocus(SpeechBox.GetNextFocus(SpeechBox.InputFocus));
            }
        }
        
        public static string GetTemplateName(HashSet<string> tags)
        {
            string pattern = @"Template:(.*)"; // Regex pattern
    
            foreach (string tag in tags)
            {
                Match match = Regex.Match(tag, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value; // Return the captured group (the value after '=')
                }
            }
            return null; // Return null if "Template=" is not found
        }
    
        public static bool ValidateLaterality(IApplicationControl Application, ISpeechBoxState SpeechBox)
        {
            IDocList grossList = SpeechBox.ActiveDocument.FindActivePart("GrossList") as IDocList;
            IDocList diagnosisList = SpeechBox.ActiveDocument.FindActivePart("DiagnosisList") as IDocList;
            if (grossList == null || diagnosisList == null) return true;
            
            List<string> mismatches = new List<string>();
            foreach (IDocListItem specimen in diagnosisList.Items)
            {
                string dxLaterality = ExtractLaterality(specimen.RenderText(RenderFlags.None));
                string grossLaterality = ExtractLaterality(grossList.Items[specimen.Index].RenderText(RenderFlags.None));
                if (grossLaterality == string.Empty || dxLaterality == string.Empty) continue;
                if (dxLaterality != grossLaterality)
                {
                    mismatches.Add(_Common.ConvertNumToLetter(specimen.Index));
                }
            }
            if (mismatches.Count > 0)
            {
                SpeechDialogSettings warning = new SpeechDialogSettings(
                    SpeechDialogSettings.IconTypes.Warning,
                    "Laterality Mismatch",
                    (mismatches.Count > 1) ? string.Format("Laterality mismatch found in parts {0}", string.Join(", ", mismatches)) : string.Format("Laterality mismatch found in part {0}", mismatches[0]));
                int ok = warning.AddChoice("Continue", "Say \"Continue\" to ignore this warning and continue sending your report.");
                int fix = warning.AddChoice("Fix", "Say \"Fix\" to return to Report Builder and fix laterality issues");
                int result = Application.ShowSpeechDialog(warning);
                
                if (result == ok) return true;
                else return false;
            }
            return true;
        }
        
        public static bool ValidateGrossDiagnosisLaterality(IApplicationControl Application, ISpeechBoxState SpeechBox)
        {
            IDocList grossList = SpeechBox.ActiveDocument.FindActivePart("GrossList") as IDocList;
            IDocList diagnosisList = SpeechBox.ActiveDocument.FindActivePart("DiagnosisList") as IDocList;
            if (grossList == null || diagnosisList == null) return true;
            
            List<string> mismatches = new List<string>();
            foreach (IDocListItem specimen in diagnosisList.Items)
            {
                string dxLaterality = ExtractLaterality(specimen.RenderText(RenderFlags.None));
                string grossLaterality = ExtractLaterality(grossList.Items[specimen.Index].RenderText(RenderFlags.None));
                if (grossLaterality == string.Empty || dxLaterality == string.Empty) continue;
                if (dxLaterality != grossLaterality)
                {
                    mismatches.Add(_Common.ConvertNumToLetter(specimen.Index+1));
                }
            }
            if (mismatches.Count > 0)
            {
                SpeechDialogSettings warning = new SpeechDialogSettings(
                    SpeechDialogSettings.IconTypes.Warning,
                    "Laterality Mismatch",
                    (mismatches.Count > 1) ? string.Format("Laterality mismatch found in parts {0}.", string.Join(", ", mismatches)) : string.Format("Laterality mismatch found in part {0}", mismatches[0]));
                int ok = warning.AddChoice("Continue", "Say \"Continue\" to ignore this warning and continue sending your report.");
                int fix = warning.AddChoice("Fix", "Say \"Fix\" to return to Report Builder and address laterality issues.");
                int result = Application.ShowSpeechDialog(warning);
                
                if (result == ok) return true;
                else return false;
            }
            return true;
        }
        
        public static bool ValidateSpecimenGrossLaterality(IApplicationControl Application, ISpeechBoxState SpeechBox, List<string> specimens)
        {
            IDocList grossList = SpeechBox.ActiveDocument.FindActivePart("GrossList") as IDocList;
            if (grossList == null || specimens.Count == 0) return true;
            
            List<string> mismatches = new List<string>();
            foreach (IDocListItem part in grossList.Items)
            {
                string specimenLaterality = ExtractLaterality(specimens[part.Index]);
                //StatusLog.WriteWarningEntry("Specimen")
                string grossLaterality = ExtractLaterality(part.RenderText(RenderFlags.None));
                if (grossLaterality == string.Empty || specimenLaterality == string.Empty) continue;
                if (specimenLaterality != grossLaterality)
                {
                    mismatches.Add(_Common.ConvertNumToLetter(part.Index+1));
                }
            }
            if (mismatches.Count > 0)
            {
                SpeechDialogSettings warning = new SpeechDialogSettings(
                    SpeechDialogSettings.IconTypes.Warning,
                    "Laterality Mismatch",
                    (mismatches.Count > 1) ? string.Format("Laterality mismatch found in parts {0}.\n\nPlease compare lateralities in the SPECIMEN SOURCE compared to your gross.", string.Join(", ", mismatches)) : string.Format("Laterality mismatch found in part {0}", mismatches[0]));
                int ok = warning.AddChoice("Continue", "Say \"Continue\" to ignore this warning and continue sending your report.  You will need to fix the SPECIMEN SOURCE manually.");
                int fix = warning.AddChoice("Fix", "Say \"Fix\" to return to Report Builder and fix laterality in Report Builder if appropriate.");
                int result = Application.ShowSpeechDialog(warning);
                
                if (result == ok) return true;
                else return false;
            }
            return true;
        }
        
        /// <summary>
        /// finds laterality descriptors in the given string and returns a standardized laterality
        /// </summary>
        /// <param name="specimenText"></param>
        /// <returns></returns>
        public static string ExtractLaterality(string specimenText)
        {
            string laterality = string.Empty;
            if (Regex.IsMatch(specimenText, @"\bLeft\b|\bL\.\b|\bL\b|\bLt\b|\bLt.\b", RegexOptions.IgnoreCase))
                laterality = "Left";
            if (Regex.IsMatch(specimenText, @"\bRight\b|\bR\.\b|\bR\b|\bRt\b|\bRt.\b", RegexOptions.IgnoreCase))
                laterality = "Right";
            if (Regex.IsMatch(specimenText, @"\bMedial\b", RegexOptions.IgnoreCase))
                laterality += "Medial";
            if (Regex.IsMatch(specimenText, @"\bTransitional\b", RegexOptions.IgnoreCase))
                laterality += "Transitional";
            if (Regex.IsMatch(specimenText, @"\bLateral\b", RegexOptions.IgnoreCase))
                laterality += "Lateral";
            if (Regex.IsMatch(specimenText, @"\bMid\b", RegexOptions.IgnoreCase))
                laterality += "Mid";
            if (Regex.IsMatch(specimenText, @"\bBase\b", RegexOptions.IgnoreCase))
                laterality += "Base";
            if (Regex.IsMatch(specimenText, @"\bApex\b", RegexOptions.IgnoreCase))
                laterality += "Apex";
            
            return laterality;
        }
        
        // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE


