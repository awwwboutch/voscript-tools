#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
//@if Word
using Word = NetOffice.WordApi;
using WordEnums = NetOffice.WordApi.Enums;
//@endif
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _{{System}} : ExtensionScript
    {
        // Everything the {{Vendor}} Core commands need that depends on HOW {{Vendor}} is driven - its windows, how it
        // shows the open case, the keystrokes that open, save and clear one. The Core commands hold the workflow and
        // are the same for every LIS; fill in the TODO: markers here, against the live application.
        //
        // Author: {{Author}}   Date: {{Date}}

        #region CASE NUMBERS

        /// <summary>
        /// An accession number as {{Vendor}} displays it. Group 1 must be the whole case number, group 2 the case type
        /// prefix - SetCaseState and every title parser below rely on that.
        /// TODO: confirm against the customer's real accession numbers (PowerPath shows S-26-00026).
        /// </summary>
        public static string CaseNumberPattern = @"(([A-Z]{1,3})-?[0-9]{2}-[0-9]{1,6})";

        /// <summary>
        /// Sets the CaseNumber and CaseType state properties. Report Builder's template lookup and the document store key
        /// off these, so every entry point (spoken case number, the LIS, the Word report, a scanner) must set both.
        /// </summary>
        public static void SetCaseState(IApplicationControl Application, string caseNumber)
        {
            Application.SetStateProperty("CaseNumber", caseNumber);
            Match match = Regex.Match(caseNumber, "^" + CaseNumberPattern + "$");
            Match prefix = Regex.Match(caseNumber, @"^([A-Za-z]{1,3})-?\d");
            Application.SetStateProperty("CaseType", match.Success ? match.Groups[2].Value : prefix.Success ? prefix.Groups[1].Value : caseNumber.Substring(0, 1));
        }

        /// <summary>
        /// Compares prefix, year and counter numerically, so "S-26-26" and "S-26-00026" are the same case - a spoken case
        /// number and the LIS's display of it rarely agree on leading zeros.
        /// </summary>
        public static bool SameCase(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string parts = @"^([A-Za-z]{1,3})-?(\d{2})-?(\d+)$";
            Match x = Regex.Match(a.Trim(), parts);
            Match y = Regex.Match(b.Trim(), parts);
            if (!x.Success || !y.Success) return a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
            return x.Groups[1].Value.Equals(y.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                && x.Groups[2].Value == y.Groups[2].Value
                && long.Parse(x.Groups[3].Value) == long.Parse(y.Groups[3].Value);
        }

        /// <summary>
        /// Report Builder template for the case: the Target column of the {{CaseTypeList}} named list for its case type
        /// (an Extended list), else {{ReportTemplate}}.
        /// </summary>
        public static string TemplateForCase(IApplicationControl Application, string caseNumber)
        {
            try
            {
                string caseType = Application.GetStateProperty("CaseType");
                TranslationParams mapped = Application.FindTranslationParams("{{CaseTypeList}}", caseType);
                if (mapped != null && !string.IsNullOrWhiteSpace(mapped.Target)) return mapped.Target;
                StatusLog.WriteWarningEntry("_{{System}} - no template mapped to case type '" + caseType + "' in {{CaseTypeList}}; using {{ReportTemplate}}");
            }
            catch (Exception ex)
            {
                StatusLog.WriteWarningEntry("_{{System}} - template lookup failed for " + caseNumber + ": " + ex.Message);
            }
            return "{{ReportTemplate}}";
        }

        #endregion

        #region LIS APPLICATION
//@if Desktop

        /// <summary>
        /// The {{Vendor}} main window's title, as a regex. TODO: confirm. A Delphi or VB6 client often owns a hidden
        /// top-level window with the same title (PowerPath's TApplication) - make sure this matches the visible one.
        /// </summary>
        public const string MainWindowPattern = "{{WindowTitle}}";

        public static bool FocusLis()
        {
            return WindowTools.Instance.EnsureForegroundWindow(MainWindowPattern);
        }

        /// <summary>
        /// The case open in {{Vendor}} right now, as {{Vendor}} formats it, or "" if none (or more than one) is showing.
        /// Default: a case number in the main window's title, else in exactly one of its child windows' titles.
        /// TODO: confirm where {{Vendor}} shows it. PowerPath titles its MDI child "Case Information - S-26-00026".
        /// </summary>
        public static string LoadedCase(IApplicationControl Application)
        {
            WindowTools WindowTools = WindowTools.Instance;
            IntPtr main = WindowTools.FindWindow(MainWindowPattern);
            if (main == IntPtr.Zero) return "";

            Match match = Regex.Match(WindowTools.GetWindowText(main), CaseNumberPattern);
            if (match.Success) return match.Groups[1].Value;

            List<string> cases = WindowTools.EnumChildWindows(main)
                .Select(child => Regex.Match(WindowTools.GetWindowText(child), CaseNumberPattern))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            return cases.Count == 1 ? cases[0] : "";
        }

        /// <summary>
        /// Opens the case in {{Vendor}} and returns its case number as {{Vendor}} shows it once it is really showing, or ""
        /// if it didn't open. Already open: returns straight away, no reload.
        /// </summary>
        public static string OpenCase(IApplicationControl Application, string caseNumber)
        {
            string alreadyLoaded = LoadedCase(Application);
            if (SameCase(alreadyLoaded, caseNumber)) return alreadyLoaded;

            FocusLis();

            // TODO: clear whatever case is showing and enter the accession. These keystrokes are {{Vendor}}-specific.
            //       PowerPath: F9 clears the case, Alt+A reaches the accession field, then paste and Tab:
            //           Automation.Send("{F9}");  ...answer its Confirm prompt...
            //           Automation.Send("!a", 200); Automation.ClipSet(caseNumber); WindowTools.Instance.Wait(150);
            //           Automation.Send("^v"); WindowTools.Instance.Wait(150); Automation.Send("{TAB}", 100);
            StatusLog.WriteErrorEntry("_{{System}}.OpenCase - TODO: the keystrokes that open a case in {{Vendor}} have not been filled in");

            return WaitForCaseLoaded(Application, caseNumber);
        }

        /// <summary>
        /// Waits for {{Vendor}} to show the case, instead of a fixed wait. A {{Vendor}} dialog that comes up on the way stops
        /// the wait - never answer a prompt blindly. TODO: once the live prompts are known, answer the expected ones by
        /// title here (PowerPath answers its Confirm prompt with Alt+Y), and keep stopping on anything else.
        /// </summary>
        public static string WaitForCaseLoaded(IApplicationControl Application, string caseNumber, int timeoutMilliseconds = 10000)
        {
            WindowTools WindowTools = WindowTools.Instance;
            IntPtr main = WindowTools.FindWindow(MainWindowPattern);
            int lisProcess = main == IntPtr.Zero ? -1 : WindowTools.GetWindowProcessID(main);
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMilliseconds);

            while (DateTime.Now < deadline)
            {
                string loaded = LoadedCase(Application);
                if (SameCase(loaded, caseNumber)) return loaded;

                IntPtr foreground = WindowTools.GetForegroundWindow();
                if (foreground != main && lisProcess != -1 && WindowTools.GetWindowProcessID(foreground) == lisProcess)
                {
                    StatusLog.WriteErrorEntry("_{{System}} - {{Vendor}} showed '" + WindowTools.GetWindowText(foreground) + "' while opening " + caseNumber);
                    return "";
                }
                WindowTools.Wait(100);
            }
            StatusLog.WriteErrorEntry("_{{System}} - case " + caseNumber + " did not open in {{Vendor}}");
            return "";
        }

        /// <summary>
        /// Saves the case in {{Vendor}} and clears it, ready for the next one (NextCase). Returns false if it couldn't.
        /// TODO: the keystrokes. PowerPath: focus the client, F10 saves, F9 clears.
        /// </summary>
        public static bool SaveAndClearCase(IApplicationControl Application)
        {
            if (!FocusLis()) return false;
            StatusLog.WriteErrorEntry("_{{System}}.SaveAndClearCase - TODO: the keystrokes that save and clear a case in {{Vendor}} have not been filled in");
            return false;
        }
//@endif
//@if Browser

        public const string BrowserClassName = "Chrome_WidgetWin_1";

        /// <summary>
        /// Window title fragments that identify a {{Vendor}} browser window. TODO: confirm against the live application.
        /// </summary>
        public static readonly List<string> WindowTitleFragments = new List<string>() { "{{WindowTitle}}" };

        public static bool FocusLis()
        {
            string title = FindCurrentTitlePage();
            return !string.IsNullOrEmpty(title) && WindowTools.Instance.EnsureForegroundWindow(Regex.Escape(title));
        }

        /// <summary>
        /// Title of the current {{Vendor}} browser window, or "" if none is open.
        /// </summary>
        public static string FindCurrentTitlePage()
        {
            foreach (IntPtr win in WindowTools.Instance.FindWindows(BrowserClassName, ""))
            {
                string windowTitle = WindowTools.Instance.GetWindowText(win);
                if (!string.IsNullOrWhiteSpace(windowTitle) && WindowTitleFragments.Any(fragment => windowTitle.Contains(fragment)))
                    return windowTitle;
            }
            return string.Empty;
        }

        /// <summary>
        /// The case open in {{Vendor}} right now, or "" if none is showing. Default: a case number in the window title.
        /// TODO: confirm. If {{Vendor}} doesn't put it in the title, read the accession label off the page with _Browser
        /// (label element, value in the next sibling) - see script-catalog.md, DictateSection.
        /// </summary>
        public static string LoadedCase(IApplicationControl Application)
        {
            Match match = Regex.Match(FindCurrentTitlePage(), CaseNumberPattern);
            return match.Success ? match.Groups[1].Value : "";
        }

        /// <summary>
        /// Opens the case in {{Vendor}} and returns its case number once it is really showing, or "" if it didn't open.
        /// TODO: the search. The IMS CaseNumber template shows the worklist-search shape (_Browser.FindElementOnPage,
        /// SetElementText, WaitForElement, click the result); a deep link is better if {{Vendor}} has stable routes.
        /// </summary>
        public static string OpenCase(IApplicationControl Application, string caseNumber)
        {
            string alreadyLoaded = LoadedCase(Application);
            if (SameCase(alreadyLoaded, caseNumber)) return alreadyLoaded;

            StatusLog.WriteErrorEntry("_{{System}}.OpenCase - TODO: opening a case in {{Vendor}} has not been filled in");
            return "";
        }

        /// <summary>
        /// Saves the case in {{Vendor}} and returns it to a blank state, ready for the next one (NextCase).
        /// TODO: the save / close controls, via _Browser.
        /// </summary>
        public static bool SaveAndClearCase(IApplicationControl Application)
        {
            StatusLog.WriteErrorEntry("_{{System}}.SaveAndClearCase - TODO: saving and clearing a case in {{Vendor}} has not been filled in");
            return false;
        }
//@endif

        #endregion
//@if Word

        #region WORD REPORT
        // {{Vendor}} edits its report in Microsoft Word. The generic Word work - one undo step, form protection, Word error
        // capture, pasting a Report Builder part - is in VOScript.Starter._Word. What is {{Vendor}}'s is below: which
        // window is the report, how it is opened, and how its sections are laid out.

        /// <summary>
        /// Title of the {{Vendor}} Word report window. Group 1 must be the case number.
        /// TODO: confirm. PowerPath's is "Case 'S-26-00026'  -  Compatibility Mode - Word", pattern "^Case '" + CaseNumberPattern.
        /// </summary>
        public static string WordTitlePattern = CaseNumberPattern + ".*Word$";

        /// <summary>
        /// The Word report window - for caseNumber if given, else any. IntPtr.Zero if none is open.
        /// </summary>
        public static IntPtr FindWordReport(string caseNumber = null)
        {
            foreach (IntPtr window in WindowTools.Instance.EnumWindows())
            {
                string caseInTitle = WordCaseNumber(window);
                if (caseInTitle != "" && (caseNumber == null || SameCase(caseInTitle, caseNumber))) return window;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Case number from a Word report window's title, or "" if it isn't one.
        /// </summary>
        public static string WordCaseNumber(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            Match match = Regex.Match(WindowTools.Instance.GetWindowText(hWnd), WordTitlePattern);
            return match.Success ? match.Groups[1].Value : "";
        }

        /// <summary>
        /// Brings up the Word report for the case open in {{Vendor}} and returns its window once Word answers automation;
        /// IntPtr.Zero if it didn't open. Already open for this case: brings it forward. Open for another case: stops.
        /// </summary>
        public static IntPtr OpenWordReport(IApplicationControl Application, string caseNumber)
        {
            WindowTools WindowTools = WindowTools.Instance;

            IntPtr open = FindWordReport();
            if (open != IntPtr.Zero)
            {
                string openCase = WordCaseNumber(open);
                if (!SameCase(openCase, caseNumber))
                {
                    ShowWarning(Application, string.Format("The Word report for case {0} is still open.\n\nPlease save or close it before dictating case {1}.", openCase, caseNumber));
                    return IntPtr.Zero;
                }
                WindowTools.EnsureForegroundWindow(open);
                return open;
            }

            // TODO: the {{Vendor}} action that opens the Word report. PowerPath: Ctrl+8 (Results tab), then click the Edit
            //       Results button - Automation.ControlClick("PowerPath", "[CLASS:TButton; INSTANCE:6]").
            FocusLis();
            StatusLog.WriteErrorEntry("_{{System}}.OpenWordReport - TODO: the action that opens the Word report in {{Vendor}} has not been filled in");

            //Poll instead of fixed waits. TODO: handle {{Vendor}}'s own prompts here by title as they are found (PowerPath:
            //an error window, an "Information" prompt for a signed-out case, and up to two Confirm prompts).
            DateTime deadline = DateTime.Now.AddSeconds(15);
            IntPtr word = IntPtr.Zero;
            while (word == IntPtr.Zero && DateTime.Now < deadline)
            {
                word = FindWordReport(caseNumber);
                if (word == IntPtr.Zero) WindowTools.Wait(100);
            }
            if (word == IntPtr.Zero)
            {
                ShowWarning(Application, "The Word report for case " + caseNumber + " did not open.  Please open it and try again.");
                return IntPtr.Zero;
            }

            _Word.WaitUntilResponds(word);      //the window shows up before Word is usable
            WindowTools.EnsureForegroundWindow(word);
            return word;
        }

        #endregion

        #region WORD REPORT LAYOUT
        // DEFAULT LAYOUT: section headers are plain text inside an editable Word section (e.g. "FINAL DIAGNOSIS:"), and a
        // section's body runs to the next header. That is PowerPath's, and it is what this implements, as tested there.
        //
        // TODO: confirm {{Vendor}}'s layout BEFORE relying on this. If its headers are protected - CoPath alternates a
        // protected header section with an editable body section - replace LocateHeaders / FillSections with a locator for
        // that layout, still going through _Word.Edit / _Word.EditableRanges / _Word.PasteDocPart.

        /// <summary>
        /// A Report Builder section carrying this tag is optional in the Word report (e.g. FROZEN SECTION): when it has
        /// dictation and its header is missing, TransferReportToWord inserts the header ahead of the next section in outline
        /// order. A missing header on an untagged section stops the transfer before Word is changed.
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
        /// Writes every dictated Report Builder section into the Word report in hWnd, under the header matching the
        /// section's Label. Every header is located before anything changes, so a missing or damaged one stops the transfer
        /// with nothing written. Returns true only when the text is in Word; on false the user has already been told why.
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
                    ShowWarning(Application, string.Format("Unable to find the following section header(s) as they are either missing or damaged:\n\n{0}\n\nPlease repair the section header(s), then re-issue this command.  Nothing was transferred.", string.Join("\n", missing)));
                    return false;
                }

                if (InsertOptionalHeaders(doc, sections))
                    LocateHeaders(doc, sections);   //positions shifted, re-read them
                FillSections(doc, sections);
                return true;
            });
        }

        /// <summary>
        /// Records where each section's header paragraph sits, searching only the editable Word sections. Match case, and
        /// the label must start its paragraph, so "comment:" in dictated text is never mistaken for COMMENT:.
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
        /// Inserts the header for each optional section that has dictation but no header in Word, directly ahead of the next
        /// section (outline order) whose header is present, or else after the last header in the report, just ahead of its
        /// section break. Plain text - the header only has to be visible.
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
        /// break, less the one paragraph mark that forms the blank line before it. Section breaks are never overwritten.
        /// Filled bottom-up so earlier offsets hold.
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
//@endif
//@if SaveToLis

        #region CASE STATUS

        /// <summary>
        /// Statuses no command may ever set: they sign the case out, which is always done by hand. TODO: confirm {{Vendor}}'s
        /// names for them.
        /// </summary>
        public static readonly string[] SignoutStatuses = { "Final", "Addendum Final", "Amendment Final", "Correction Final" };

        public static bool IsSignoutStatus(string status)
        {
            return SignoutStatuses.Any(s => s.Equals((status ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        #endregion
//@endif
//@if Page

        #region REPORT FIELDS
        // {{Vendor}} edits its report in its own page, not in Word.

        /// <summary>
        /// Writes one Report Builder section into the {{Vendor}} report field whose name matches the section's Label.
        /// Returns false if no field matched - the caller reports it and does not release the document.
        /// TODO: the addressing. The IMS ReturnToSystem template shows the browser shape: find the report panel with
        /// _Browser.FindElementOnPage, _Browser.GetChildByName(panel, part.Label), then _Browser.SetElementText. Use
        /// RenderFlags.None for a rich editor, RenderFlags.Text for a plain textarea - the wrong one silently empties it.
        /// </summary>
        public static bool WriteReportSection(IApplicationControl Application, IDocPart part)
        {
            StatusLog.WriteErrorEntry("_{{System}}.WriteReportSection - TODO: writing '" + part.Label + "' into {{Vendor}} has not been filled in");
            return false;
        }

        /// <summary>
        /// Report Builder sections in outline order, minus nested ones (e.g. Synoptic inside Diagnosis): those render with
        /// their parent.
        /// </summary>
        public static List<IDocPart> TopLevelSections(IDocument document)
        {
            List<IDocPart> parts = new List<IDocPart>();
            foreach (string key in document.EnumerateParts(DocPartTypes.Section))
            {
                IDocPart part = document.FindActivePart(key, DocPartTypes.Section);
                if (part != null) parts.Add(part);
            }
            HashSet<string> nested = new HashSet<string>();
            foreach (IDocPart part in parts)
                foreach (string childKey in part.EnumerateParts(DocPartTypes.Section))
                    if (childKey != part.PartKey) nested.Add(childKey);
            return parts.Where(p => !nested.Contains(p.PartKey)).ToList();
        }

        /// <summary>
        /// RenderText includes list part labels, so an untouched GrossPart renders "A." - strip those before testing, or
        /// an empty section overwrites what the pathologist typed with a bare part letter.
        /// </summary>
        public static bool HasDictation(IDocPart part)
        {
            string text = Regex.Replace(part.RenderText(RenderFlags.Text), @"(?m)^\s*[A-Z]{1,2}\d{0,2}\.\s*$", "");
            return text.Any(c => Char.IsLetterOrDigit(c));
        }

        #endregion
//@endif

        public static void ShowWarning(IApplicationControl Application, string message)
        {
            SpeechDialogSettings dialog = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "VoiceOver PRO - {{Vendor}}", message);
            dialog.AddChoice("OK", "Say \"OK\" to continue.");
            Application.ShowSpeechDialog(dialog);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
