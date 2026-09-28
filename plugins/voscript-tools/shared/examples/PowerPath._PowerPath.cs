#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using System.Text.RegularExpressions;
using Word = NetOffice.WordApi;
using WordEnums = NetOffice.WordApi.Enums;
#endregion USING NAMESPACES

namespace VOScript.Starter.PowerPath
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _PowerPath : ExtensionScript
    {
        public static string caseNumberPattern = @"(([A-Z]{1,3})-[0-9]{2}-[0-9]{5,})";
        
        /// <summary>
        /// Title of the PowerPath Word report window ("Case 'S-26-00026'  -  Compatibility Mode - Word"). Group 1 is
        /// the case number, group 2 the case type.
        /// </summary>
        public static string WordTitlePattern = "^Case '" + caseNumberPattern;
        
        public static string GetCaseNumberPowerPath(IApplicationControl Application)
        {
            WindowTools WindowTools = WindowTools.Instance;
            Match caseNumberMatch = null;
            string caseNumber = null;
            string caseType = null;
            
            if (WindowTools.FindWindow("Revision Reason") != IntPtr.Zero)
            {
                WindowTools.EnsureForegroundWindow("Revision Reason");
                WindowTools.Wait(100);
                Automation.Send("!o", 2000);
            }

            //if PowerPath case Word not found is not found
            _PowerPath.FocusPowerPath();
            List<IntPtr> children = WindowTools.EnumChildWindows(WindowTools.FindWindow("PowerPath"));
            List<IntPtr> matches = new List<IntPtr>();
            foreach (IntPtr child in children)      //this checks each child window in the PowerPath Client window
            {
                if (WindowTools.GetWindowText(child).Contains("Case Information"))
                {
                    matches.Add(child);
                }
            }
            #region handling one case, multiple cases, or no cases open
            if (matches.Count > 1)
            {
                SpeechDialogSettings settings = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "VoiceOver PRO - PowerPath", "Failure to maximize the case in PowerPath.\n\nMultiple cases are open in PowerPath.  Which case would you like to dictate?");
                foreach (IntPtr match in matches)
                {
                    caseNumberMatch = Regex.Match(WindowTools.GetWindowText(match), caseNumberPattern);
                    string matchValue = caseNumberMatch.Groups[1].Value;
                    settings.AddChoice("choose " + (matches.IndexOf(match) + 1).ToString(), matchValue);
                }
                int result = Application.ShowSpeechDialog(settings);
                if (result < 0) return string.Empty;
                WindowTools.SetForegroundWindow(matches[result]);
                //Automation.Send("!-x", 1000);
                WindowTools.Wait(100);
                caseNumberMatch = Regex.Match(WindowTools.GetWindowText(matches[result]), caseNumberPattern);
            }
            else if (matches.Count == 1)
            {
                WindowTools.SetForegroundWindow(matches[0]);
                caseNumberMatch = Regex.Match(WindowTools.GetWindowText(matches[0]), caseNumberPattern);
            }

            if (caseNumberMatch == null || !caseNumberMatch.Success)     //null when no Case Information window is open
            {
                SpeechDialogSettings settings = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "VoiceOver PRO - PowerPath", "Unable to capture the case number from PowerPath.  Please ensure a case is open and try again.");
                int ok = settings.AddChoice("OK", "Press OK to close this dialog");
                Application.ShowSpeechDialog(settings);
                StatusLog.WriteErrorEntry("ERROR: Failed to capture the accession number from PowerPath.");
                return string.Empty;
            }
            #endregion
            caseNumber = caseNumberMatch.Groups[1].Value.Trim();
            caseType = caseNumberMatch.Groups[2].Value.Trim();
            
            Application.SetStateProperty("CaseNumber", caseNumber);
            Application.SetStateProperty("CaseType", caseType);

            return caseNumber;

        } // close Execute
        
        public static string GetCaseNumberWord(IApplicationControl Application)
        {
            WindowTools WindowTools = WindowTools.Instance;
            Match caseNumberMatch = null;
            string caseNumber = null;
            string caseType = null;
            WindowTools.EnsureForegroundWindow("^Case '" + caseNumberPattern);

            //Capturing case number and case prefix from header of window using a RegEx match.  Groups[1] returns the portion of the match in parentheses
            caseNumberMatch = Regex.Match(WindowTools.GetWindowText(WindowTools.GetForegroundWindow()), caseNumberPattern);

            caseNumber = caseNumberMatch.Groups[1].Value;
            caseType = caseNumberMatch.Groups[2].Value;
            if (caseNumber.Length < 1)
            {
                SpeechDialogSettings caseNumFail = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "VoiceOver PRO - PowerPath", "Unable to capture the case number from PowerPath.  Unable to open document.\n\nIf 'Results' button has already been pressed to open case in Word, please make sure Word is not minimized, then try the command again.");
                int ok = caseNumFail.AddChoice("OK", "Press OK to close this dialog");
                Application.ShowSpeechDialog(caseNumFail);
                StatusLog.WriteErrorEntry("Failed to capture case number. Command aborted.");
                return string.Empty;
            }
            return caseNumber;
        }
        
        public static bool FocusPowerPath()
        {
            return WindowTools.Instance.EnsureForegroundWindow("PowerPath");
        }
        
        public static void MaximizeChildWindow()
        {
            Automation.Send("!-x");
        }
        
        public static void MaximizeChildWindow(IntPtr childHandle)
        {
            WindowTools.Instance.EnsureForegroundWindow(childHandle);
            Automation.Send("!-x");
        }
        
        /// <summary>
        /// Sends ^6 to activate the History tab in Powerpath
        /// </summary>
        public static void FocusHistory()
        {
            Automation.Send("^6");
        }
        
        /// <summary>
        /// Sends ^1 to activate the General tab in Powerpath
        /// </summary>
        public static void FocusGeneral()
        {
            Automation.Send("^1");
        }
        
        /// <summary>
        /// Sends ^8 to activate the Results tab in Powerpath
        /// </summary>
        public static void FocusResults()
        {
            Automation.Send("^8");
        }
        
        /// <summary>
        /// Sends ^7 to activate the Concurrent tab in Powerpath
        /// </summary>
        public static void FocusConcurrent()
        {
            Automation.Send("^7");
        }
        
        /// <summary>
        /// Sends ^0 to activate the Notes tab in Powerpath
        /// </summary>
        public static void FocusNotes()
        {
            Automation.Send("^0");
        }
        
        /// <summary>
        /// Sends ^5 to activate the Patient tab in Powerpath
        /// </summary>
        public static void FocusPatient()
        {
            Automation.Send("^5");
        }
        
        /// <summary>
        /// Sends ^9 to activate the Images tab in Powerpath
        /// </summary>
        public static void FocusImages()
        {
            Automation.Send("^9");
        }
        
        /// <summary>
        /// Sends ^3 to activate the Specimens tab in Powerpath
        /// </summary>
        public static void FocusSpecimens()
        {
            Automation.Send("^3");
        }
        
        /// <summary>
        /// Sends ^2 to activate the Requisition tab in Powerpath
        /// </summary>
        public static void FocusRequisition()
        {
            Automation.Send("^2");
        }
        
        /// <summary>
        /// Sends ^4 to activate the Charges tab in Powerpath
        /// </summary>
        public static void FocusCharges()
        {
            Automation.Send("^4");
        }
        
        /// <summary>
        /// Used in Word Document to send F10 to start the Return to PowerPath process 
        /// </summary>
        public static void SaveCase()
        {
            Automation.Send("{F10}");
            WindowTools.Instance.Wait(400);
        }
        
        /// <summary>
        /// Used in PowerPath Client to clear out the current case
        /// </summary>
        public static void ClearCase()
        {
            Automation.Send("{F9}", 1500);
        }
        
        /// <summary>
        /// Brings the PowerPath Word document into focus
        /// </summary>
        public static void FocusWordDoc()
        {
            WindowTools.Instance.EnsureForegroundWindow("^Case '");
        }
        
        /// <summary>
        /// In PowerPath with Word Add-In (used sometimes by gross and transcription), opens the case search dialog
        /// </summary>
        public static void LoadCase()
        {
            Automation.Send("{F12}");
        }
        
        /// <summary>
        /// Launches the Orders dialog in PowerPath Client
        /// </summary>
        public static void NewOrder()
        {
            Automation.Send("^o");
        }
        
        /// <summary>
        /// Presses the Edit Results button on Results tab of PowerPath Client
        /// </summary>
        public static void PressEdit()
        {
            Automation.ControlClick("PowerPath", "[CLASS:TButton; INSTANCE:6]");  //pressing the Edit Results button.  This avoids a weird error prompt from PPath that !u seems to trigger
        }
        
        /// <summary>
        /// Presses the Signout button on the results tab of PowerPath Client
        /// </summary>
        public static void PressSignout()
        {
            Automation.Send("!s");
        }
        
        #region WORD REPORT LAYOUT
        // PowerPath's report: section headers are plain text (e.g. "FINAL DIAGNOSIS:") inside the editable Word section,
        // a section's body runs to the next header, and the demographics sit in a form-protected Word section above.
        // The generic Word work (undo, protection, error capture, pasting) is in VOScript.Starter._Word.
        
        /// <summary>
        /// A Report Builder section carrying this tag is optional in the Word report (e.g. FROZEN SECTION): when it has
        /// dictation and its header is missing, TransferReportToWord inserts the header ahead of the next section in
        /// outline order. A missing header on an untagged section stops the transfer before Word is changed.
        /// </summary>
        public const string OptionalTag = "optional";
        
        class ReportSection
        {
            public IDocPart Part;
            public string Label;
            public bool HasDictation;
            public int HeaderStart = -1;    // start of the header paragraph in Word; -1 = not found
            public int BodyStart = -1;      // end of the header paragraph = first character of the section body
            public int ScopeEnd = -1;       // end of the editable Word section the header sits in
            public bool Found { get { return HeaderStart >= 0; } }
            public bool IsOptional { get { return Part.TagSet.Contains(OptionalTag); } }
        }
        
        /// <summary>
        /// Writes every dictated Report Builder section into the PowerPath Word report in hWnd, under the header
        /// matching the section's Label. Every header is located before anything changes, so a missing or damaged one
        /// stops the transfer with nothing written. Returns true only when the text is in Word; on false the user has
        /// already been told why.
        /// </summary>
        public static bool TransferReportToWord(IApplicationControl Application, IntPtr hWnd, IDocument document)
        {
            List<ReportSection> sections = _Word.TopLevelSections(document)
                .Select(p => new ReportSection { Part = p, Label = p.Label.Trim(), HasDictation = _Word.HasDictation(p) })
                .ToList();
            
            return _Word.Edit(Application, hWnd, "VoiceOver PRO Return to Word", doc =>
            {
                LocateHeaders(doc, sections);
                
                //Check every header up front so a damaged one never leaves the case half-transferred
                List<string> missing = sections.Where(s => s.HasDictation && !s.Found && !s.IsOptional).Select(s => s.Label).ToList();
                if (missing.Count > 0)
                {
                    StatusLog.WriteInformationEntry("ReturnToWord - section headers not found in Word: " + string.Join(" | ", missing));
                    SpeechDialogSettings notFound = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "Section not found",
                        string.Format("Unable to find the following section header(s) as they are either missing or damaged:\n\n{0}\n\nPlease repair the section header(s), then re-issue this command.  Nothing was transferred.", string.Join("\n", missing)));
                    notFound.AddChoice("OK", "Say \"OK\" to continue.");
                    Application.ShowSpeechDialog(notFound);
                    return false;
                }
                
                if (InsertOptionalHeaders(doc, sections))
                    LocateHeaders(doc, sections);   //positions shifted, re-read them
                FillSections(doc, sections);
                return true;
            });
        }
        
        /// <summary>
        /// Records where each section's header paragraph sits, searching only the editable Word sections. Match case,
        /// and the label must start its paragraph, so "comment:" in dictated text is never mistaken for COMMENT:.
        /// </summary>
        static void LocateHeaders(Word.Document doc, List<ReportSection> sections)
        {
            List<int[]> scopes = _Word.EditableRanges(doc).Select(r => new int[] { r.Start, r.End }).ToList();
            foreach (ReportSection section in sections)
            {
                section.HeaderStart = -1;
                section.BodyStart = -1;
                section.ScopeEnd = -1;
                
                foreach (int[] scope in scopes)
                {
                    int searchFrom = scope[0];
                    while (searchFrom < scope[1])
                    {
                        Word.Range hit = doc.Range(searchFrom, scope[1]);
                        Word.Find find = hit.Find;
                        find.ClearFormatting();
                        find.Text = section.Label;
                        find.MatchCase = true;
                        find.MatchWildcards = false;
                        find.Forward = true;
                        find.Wrap = WordEnums.WdFindWrap.wdFindStop;
                        find.Format = false;
                        if (!find.Execute()) break;
                        
                        Word.Range paragraph = hit.Paragraphs.First.Range;
                        string lead = doc.Range(paragraph.Start, hit.Start).Text;
                        if (string.IsNullOrWhiteSpace(lead))
                        {
                            section.HeaderStart = paragraph.Start;
                            section.BodyStart = paragraph.End;
                            section.ScopeEnd = scope[1];
                            break;
                        }
                        searchFrom = hit.End;
                    }
                    if (section.Found) break;
                }
            }
        }
        
        /// <summary>
        /// Inserts the header for each optional section that has dictation but no header in Word, directly ahead of the
        /// next section (outline order) whose header is present, or else after the last header in the report, just
        /// ahead of its section break. Plain text - the header only has to be visible. Leaves an empty body paragraph,
        /// matching the layout of the stock headers.
        /// </summary>
        static bool InsertOptionalHeaders(Word.Document doc, List<ReportSection> sections)
        {
            bool inserted = false;
            for (int i = 0; i < sections.Count; i++)
            {
                ReportSection section = sections[i];
                if (section.Found || !section.HasDictation || !section.IsOptional) continue;
                
                ReportSection anchor = sections.Skip(i + 1).FirstOrDefault(s => s.Found);
                if (anchor != null)
                {
                    doc.Range(anchor.HeaderStart, anchor.HeaderStart).InsertBefore(section.Label + "\r\r\r");
                }
                else
                {
                    ReportSection last = sections.Where(s => s.Found).OrderBy(s => s.HeaderStart).LastOrDefault();
                    int end = last != null ? last.ScopeEnd - 1 : _Word.EditableRanges(doc).Last().End - 1;
                    doc.Range(end, end).InsertBefore(section.Label + "\r\r");
                }
                inserted = true;
                LocateHeaders(doc, sections);   //later anchors moved down
            }
            return inserted;
        }
        
        /// <summary>
        /// Replaces each dictated section's body with the Report Builder text. A body runs from the end of its header
        /// paragraph to the next header in the same Word section (by position, not outline order) or to that section's
        /// break, less the one paragraph mark that forms the blank line before it. Section breaks are never
        /// overwritten. Filled bottom-up so earlier offsets hold.
        /// </summary>
        static void FillSections(Word.Document doc, List<ReportSection> sections)
        {
            List<ReportSection> found = sections.Where(s => s.Found).OrderBy(s => s.HeaderStart).ToList();
            
            for (int i = found.Count - 1; i >= 0; i--)
            {
                ReportSection section = found[i];
                if (!section.HasDictation) continue;
                
                bool nextInScope = i + 1 < found.Count && found[i + 1].HeaderStart < section.ScopeEnd;
                int boundary = nextInScope ? found[i + 1].HeaderStart : section.ScopeEnd - 1;
                _Word.PasteDocPart(doc, section.BodyStart, Math.Max(section.BodyStart, boundary - 1), section.Part);
            }
        }
        
        #endregion

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE

