// PREREQUISITE: VOScript.Starter._Word
// Source revision 1D-3650-2 (a DRAFT, live-tested with PowerPath 2026-09-28 - publish it in the source tenant), exported 2026-09-28 from demo\sales.
// Tier: word
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VoiceOver.Common;
using VoiceOver.Extensions;
using Word = NetOffice.WordApi;
using WordEnums = NetOffice.WordApi.Enums;
#endregion USING NAMESPACES

namespace VOScript.Starter
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _Word : ExtensionScript
    {
        // Generic helpers for any LIS that uses Microsoft Word as its report editor. Everything here is NetOffice
        // through MSWordTools.GetWordApplication - Ranges, no Selection moves or keystrokes, so Word never has to be
        // in the foreground.
        //
        // How a report LAYS OUT its sections is not here: that differs per LIS (PowerPath has plain-text headers in
        // one editable section; CoPath alternates protected header sections with editable body sections), so each
        // LIS library locates its own section bodies and uses these helpers to do the editing.

        /// <summary>
        /// Runs an edit against the Word document in hWnd: screen updating off, one undo step (a single Ctrl+Z
        /// reverses the lot), and any Word failure captured and shown to the user. Returns what edit returned, or
        /// false if Word threw. Always go through this - a NetOffice exception that escapes the script reaches PRO
        /// only as "SerializationException: MethodCOMException is not marked as serializable", hiding the cause.
        /// </summary>
        public static bool Edit(IApplicationControl Application, IntPtr hWnd, string undoName, Func<Word.Document, bool> edit)
        {
            Word.Application word = null;
            try
            {
                word = MSWordTools.Instance.GetWordApplication(hWnd);
                Word.Document doc = word.ActiveDocument;

                word.ScreenUpdating = false;
                word.UndoRecord.StartCustomRecord(undoName);
                return edit(doc);
            }
            catch (Exception ex)
            {
                string detail = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
                StatusLog.WriteErrorEntry("_Word - Word automation failed: " + detail);
                ShowWarning(Application, "Transfer to Word failed",
                    string.Format("Word reported an error while updating the report:\n\n{0}\n\nAny partial changes can be undone in Word with a single Ctrl+Z.", detail));
                return false;
            }
            finally
            {
                try
                {
                    if (word != null)
                    {
                        if (word.UndoRecord.IsRecordingCustomRecord) word.UndoRecord.EndCustomRecord();
                        word.ScreenUpdating = true;
                    }
                }
                catch (Exception ex) { StatusLog.WriteErrorEntry("_Word - could not restore Word after the edit: " + ex.Message); }
            }
        }

        /// <summary>
        /// The Word sections the user can type in. LIS report templates are commonly form-protected (demographics,
        /// and on some systems the section headers), and Find or edits over protected text throw. Each range ends
        /// with its section break, or the document's final paragraph mark - stop one character short of End to
        /// leave that in place.
        /// </summary>
        public static List<Word.Range> EditableRanges(Word.Document doc)
        {
            List<Word.Range> ranges = new List<Word.Range>();
            WordEnums.WdProtectionType protection = doc.ProtectionType;
            for (int i = 1; i <= doc.Sections.Count; i++)
            {
                Word.Section wordSection = doc.Sections[i];
                bool editable = protection == WordEnums.WdProtectionType.wdNoProtection
                    || (protection == WordEnums.WdProtectionType.wdAllowOnlyFormFields && !wordSection.ProtectedForForms);
                if (editable) ranges.Add(wordSection.Range);
            }
            return ranges;
        }

        /// <summary>
        /// Replaces Word's text from start to end with the Report Builder part, formatting included.
        /// </summary>
        public static void PasteDocPart(Word.Document doc, int start, int end, IDocPart part)
        {
            part.RenderToClipboard(RenderFlags.None);
            WindowTools.Instance.Wait(50);
            doc.Range(start, end).Paste();
        }

        /// <summary>
        /// Report Builder sections in outline order, minus nested ones (e.g. Synoptic inside Diagnosis): those
        /// render with their parent and have no place of their own in the Word report.
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
        /// RenderText includes list part labels, so an untouched GrossPart renders "A." - strip those before
        /// testing, or an empty section overwrites the [X] placeholder with a bare part letter.
        /// </summary>
        public static bool HasDictation(IDocPart part)
        {
            string text = Regex.Replace(part.RenderText(RenderFlags.Text), @"(?m)^\s*[A-Z]{1,2}\d{0,2}\.\s*$", "");
            return text.Any(c => Char.IsLetterOrDigit(c));
        }

        /// <summary>
        /// A report window appears before Word is usable. Waits until Word answers automation for it, instead of a
        /// fixed wait. Returns false (and logs) if it didn't within the timeout; callers generally carry on anyway.
        /// </summary>
        public static bool WaitUntilResponds(IntPtr hWnd, int timeoutMilliseconds = 5000)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.Now < deadline)
            {
                try
                {
                    Word.Application word = MSWordTools.Instance.GetWordApplication(hWnd);
                    if (word != null && word.ActiveDocument != null && !string.IsNullOrEmpty(word.ActiveDocument.Name)) return true;
                }
                catch (Exception) { }   //"call rejected" etc. while Word is still loading
                WindowTools.Instance.Wait(100);
            }
            StatusLog.WriteInformationEntry("_Word - Word did not answer automation within " + timeoutMilliseconds + " ms; continuing");
            return false;
        }

        static void ShowWarning(IApplicationControl Application, string title, string message)
        {
            SpeechDialogSettings dialog = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, title, message);
            dialog.AddChoice("OK", "Say \"OK\" to continue.");
            Application.ShowSpeechDialog(dialog);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
