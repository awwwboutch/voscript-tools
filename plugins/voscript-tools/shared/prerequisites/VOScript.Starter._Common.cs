// PREREQUISITE: VOScript.Starter._Common
// Source revision 2P-3445-12, exported 2026-09-28 from demo\sales.
// Tier: reporting
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _Common : ExtensionScript
    {
        /// <summary>
        /// This function converts a letter to its associated number (A > 1, B > 2, etc)
        /// </summary>
        /// <param name="toConvert"></param>
        /// <returns></returns>
        public static string ConvertNumToLetter(int toConvert)
        {
            toConvert++;
            int getRemain = toConvert % 26 == 0 ? 26 : toConvert % 26;
            int getWhole = getRemain == 26 ? (toConvert / 26) - 1 : toConvert / 26;
            string firstLetter = getWhole > 0 ? Convert.ToChar(64 + getWhole).ToString() : string.Empty;
            string secondLetter = Convert.ToChar(64 + getRemain).ToString();  
            
            StatusLog.WriteInformationEntry("first letter "+ firstLetter + "Secondletter " + secondLetter + " input " + toConvert);
            return firstLetter+secondLetter;
        } // close Execute
        
        /// <summary>
        /// This function will copy the current selection to the clipboard and ensure that the clipboard updates successully via a loop.  It will return the new clipboard contents as a string.
        /// </summary>
        /// <returns></returns>
        public static string ClipboardUpdate()
        {
            int counter = 0;
            string clipboardContents = "";
            while (counter < 7 && clipboardContents == "")
            {
                counter++;
                try
                {
                    WindowTools.Instance.FlushClipboardBuffer();
                    Automation.Send("^c", 250);
                    Automation.Send("^c", 250);
                    WindowTools.Instance.FlushClipboardBuffer();
                    clipboardContents = Automation.ClipGet();
                    WindowTools.Instance.Wait(50);
                }
                catch { }
            }
            return clipboardContents;
        }
        
        /// <summary>
        /// Copies the current selection and reliably returns the copied text. Seeds the clipboard
        /// with a unique sentinel, issues the copy, then polls until the clipboard changes away from
        /// the sentinel (proving the copy landed instead of returning stale contents). Optionally
        /// validates each read against a caller-supplied predicate before accepting it.
        /// </summary>
        /// <param name="isValid">Optional validator, e.g. s => Regex.IsMatch(s, @"^\d{2}-\d{6}$").
        /// When supplied, a read is only accepted if it passes; otherwise the copy is re-issued.</param>
        /// <param name="maxAttempts">How many times to re-issue the copy if the read never settles.</param>
        /// <param name="settleTimeoutMs">How long to poll for the clipboard to change, per attempt.</param>
        /// <param name="pollIntervalMs">Polling interval while waiting for the clipboard to settle.</param>
        /// <returns>The trimmed clipboard text, or "" if every attempt failed.</returns>
        public static string ClipboardUpdate(
            Func<string, bool> isValid = null,
            int maxAttempts = 5,
            int settleTimeoutMs = 1500,
            int pollIntervalMs = 40)
        {
            Exception lastError = null;
        
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Unique value the real selection will never accidentally equal.
                string sentinel = "" + Guid.NewGuid().ToString("N");
        
                try
                {
                    // Seed the clipboard. This also replaces the flush — ClipPut overwrites the buffer.
                    Automation.ClipSet(sentinel);
        
                    // Issue the copy. The second send guards against the first being eaten by a
                    // focus change; drop it if your target reliably copies on the first try.
                    Automation.Send("^c", 100);
                    Automation.Send("^c", 100);
        
                    // Poll until the clipboard moves off the sentinel (copy landed) or we time out.
                    int waited = 0;
                    while (waited < settleTimeoutMs)
                    {
                        string current;
                        try { current = Automation.ClipGet() ?? ""; }
                        catch (Exception ex) { lastError = ex; current = sentinel; } // clipboard busy -> keep polling
        
                        current = current.Trim();
        
                        if (current.Length > 0 && current != sentinel)
                        {
                            if (isValid == null || isValid(current))
                                return current;
        
                            break; // got data but it failed validation (stale/garbage) -> re-copy
                        }
        
                        WindowTools.Instance.Wait(pollIntervalMs);
                        waited += pollIntervalMs;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex; // usually OpenClipboard contention -> retry whole attempt
                }
            }
        
            // Surface the failure instead of returning "" silently:
            // Logger.Warn($"ClipboardUpdate failed after {maxAttempts} attempts", lastError);
            return "";
        }
        
        /// <summary>
        /// Copies the current selection, treating "nothing got copied" as an empty field rather than
        /// a failure to retry. Returns the trimmed value, "" for an empty field, or null if the
        /// clipboard was genuinely inaccessible (so you can tell empty apart from broken).
        /// </summary>
        public static string ReadFieldAllowEmpty(int settleTimeoutMs = 1200, int pollIntervalMs = 40)
        {
            string sentinel = "__VO_CLIP__" + Guid.NewGuid().ToString("N");
            try
            {
                Automation.ClipSet(sentinel);
                Automation.Send("^c", 100);
        
                int waited = 0;
                while (waited < settleTimeoutMs)
                {
                    string current;
                    try { current = Automation.ClipGet() ?? ""; }
                    catch { current = sentinel; } // clipboard busy -> keep polling
        
                    // Anything other than the sentinel means the field had content.
                    if (current != sentinel)
                        return current.Trim();
        
                    WindowTools.Instance.Wait(pollIntervalMs);
                    waited += pollIntervalMs;
                }
        
                // Sentinel survived the whole window -> nothing was copied -> empty field.
                return string.Empty;
            }
            catch
            {
                return null; // genuinely couldn't touch the clipboard
            }
        }
        
        public static bool SendCaseNumberIntoEditor(ISpeechBoxState SpeechBox, ITextEditorState TextEditor, string caseNumber)
        {
            WindowTools WindowTools = WindowTools.Instance;
            if (SpeechBox.ActiveDocument != null)
            {
                IDocField focus = SpeechBox.InputFocus;
                if (focus == null) return true;
                string focusText = SpeechBox.InputFocus.Value;
                focus.Value = focusText.Trim() + " " + caseNumber;
                SpeechBox.RefreshDocumentChanges();
                SpeechBox.SetInputFocus(focus);
                Automation.Send("{RIGHT}");
                return true;
            }
            else if (TextEditor.ActiveDocument != null)
            {
                Automation.Send("^c", 150);
                WindowTools.FlushClipboardBuffer();
                string selection = Automation.ClipGet();
                if (selection.StartsWith("[") && selection.EndsWith("]"))
                    TextEditor.InsertPlainText(caseNumber);
                else
                {
                    if (selection.Length < 1)
                    {
                        Automation.Send("+{LEFT}", 25);
                        WindowTools.FlushClipboardBuffer();
                        Automation.Send("^c", 150);
                        WindowTools.FlushClipboardBuffer();
                        selection = Automation.ClipGet();
                        Automation.Send("{RIGHT}", 25);
                        if (selection != " ")
                            Automation.Send("{SPACE}", 25);    
                        Automation.Send(caseNumber);
                    }
                }
                return true;
            }
            return false;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE

