#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Extensions;
using System.Windows;
using System.Text.RegularExpressions;
using VoiceOver.Common;
#endregion USING NAMESPACES

namespace VOScript.Starter.PowerPath.Core
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
  [ExtensionCommandClass(HelpText = "Will enter the spoken case number. Standard Trigger: <CaseType> [<Year> | <Digit> <Digit> dash |] [<Digit>|1-5]")]
  [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of case number counter part.")]
  [ExtensionStringProperty("CaseTypeList", DefaultValue = "CaseType", HelpText = "String name the case type named list.")]
  [ExtensionBoolProperty("LeadingZeros", DefaultValue = false, HelpText = "Determines if case number is constructed with or without leading zeros.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseNumber : CommandScript
    {
        public override void Execute()
        {
            //Setup to work with PowerPath Client 12.0.0162.0
            string caseNumber = _CaseNumber.FormatParameterizedSpeech(SpeechParams, Property("CounterLength", 5), Property("CaseTypeList", "CaseType"), Property("LeadingZeros", true));
            if (string.IsNullOrEmpty(caseNumber)) return;

            //Report Builder open: the case number is dictated text (e.g. a referenced case), so it must not replace the current case's state
            if (SpeechBox.ActiveDocument != null)
            {
                IDocField focus = SpeechBox.InputFocus;
                if (focus == null) return;
                string focusText = SpeechBox.InputFocus.Value;
                focus.Value = focusText.Trim() + " " + caseNumber;
                SpeechBox.RefreshDocumentChanges();
                SpeechBox.SetInputFocus(focus);
                Automation.Send("{RIGHT}");
                return;
            }

            SetCaseState(caseNumber);

            //A Word report still open would be caught mid-edit by clearing the case; same case means it's already up
            IntPtr openWord = WindowTools.FindWindow("^Case '" + _PowerPath.caseNumberPattern);
            if (openWord != IntPtr.Zero)
            {
                Match openMatch = Regex.Match(WindowTools.GetWindowText(openWord), "^Case '" + _PowerPath.caseNumberPattern);
                string openCase = openMatch.Success ? openMatch.Groups[1].Value : "";
                if (!SameCase(openCase, caseNumber))
                {
                    ShowWarning(string.Format("The Word report for case {0} is still open.\n\nPlease save or close it before opening case {1}.", openCase, caseNumber));
                    return;
                }
                SetCaseState(openCase);
                DictatePowerPath_Logic.Execute(Application, SpeechBox, DocumentStore, TextDocumentStore, TextEditor, SpeechParams, true);
                return;
            }

            string loadedCase = LoadCase(caseNumber);
            if (loadedCase == "") return;
            SetCaseState(loadedCase);   //PowerPath's own formatting, so later comparisons against Word's title match

            Automation.Send("!n", 150);    // put the cursor into the PATIENT name field
            string tabnumber = Application.GetStateProperty("AutoTab");     // Retrieve AUTOTAB Property from ROLE, this property needs to be USER specific.
            if (!string.IsNullOrEmpty(tabnumber)) Automation.Send("^" + tabnumber, 500);  // Navigates to the desired tab in Powerpath, e.g. ^8

            DictatePowerPath_Logic.Execute(Application, SpeechBox, DocumentStore, TextDocumentStore, TextEditor, SpeechParams, true);
        }

        /// <summary>
        /// Gets the case open in PowerPath's Case Information window and returns the case number as PowerPath shows it,
        /// or "" if it didn't open. Skips the clear/reload when the case is already the one showing.
        /// </summary>
        string LoadCase(string caseNumber)
        {
            _PowerPath.FocusPowerPath();

            if (WindowTools.IsForegroundWindow("Desktop of") || WindowTools.IsForegroundWindow("Pathology Neighborhood"))   // If user says command from "Desktop of <user>" screen, switch to Cases prior to sending accession number
            {
                StatusLog.WriteInformationEntry("Desktop of User found, navigate to Case Information window.");
                Automation.Send("^{F2}", 100);
                WindowTools.WaitForWindow("Pathology Neighborhood", 3000);
                Automation.Send("{HOME}", 100);
                Automation.Send("Cases", 100);
                Automation.Send("{ENTER}", 100);
                WindowTools.WaitForWindow("Case Information", 3000);
            }
            else if (WindowTools.IsForegroundWindow("Load Case"))     // For use in PowerPath w/Word Add-in
            {
                StatusLog.WriteInformationEntry("Load case window found.");
                Automation.Send("!a");
                Automation.Send("{HOME}+{END}");
                Automation.Send(caseNumber);
                Automation.Send("{ENTER}");
                return "";
            }

            IntPtr mainWindow = WindowTools.GetForegroundWindow();
            string alreadyLoaded = LoadedCase(mainWindow);
            if (SameCase(alreadyLoaded, caseNumber)) return alreadyLoaded;

            if (WindowTools.IsForegroundWindow("Case Information"))
            {
                if (!WindowTools.GetWindowText(mainWindow).Contains("New Cases")) ClearCurrentCase();
                // Placing the cursor in the Accession Number field, sending the case number, tabbing to open case
                Automation.Send("!a", 200);
                Automation.ClipSet(caseNumber);
                WindowTools.Wait(150);
                Automation.Send("^v");
                WindowTools.Wait(150);
                Automation.Send("{TAB}", 100);
            }
            else if (WindowTools.IsForegroundWindow("Case Explorer"))
            {
                StatusLog.WriteInformationEntry("Case Explorer window found.");
                // need to make sure cursor focus is in worklist before sending accession number
                Automation.Send(caseNumber, 100);
                WindowTools.Wait(500);
                Automation.Send("{ENTER}", 100);
            }
            else
            {
                ShowWarning("Unable to open the case.\n\nPlease bring up the Case Information window or Case Explorer in PowerPath and try again.");
                return "";
            }

            return WaitForCaseLoaded(caseNumber, mainWindow);
        }

        /// <summary>
        /// F9 clears the current case; answers PowerPath's Confirm / Client Error prompts as they appear instead of
        /// fixed waits. Carries on even if the clear can't be confirmed - WaitForCaseLoaded checks the result anyway.
        /// </summary>
        void ClearCurrentCase()
        {
            Automation.Send("{F9}");
            DateTime deadline = DateTime.Now.AddSeconds(4);
            while (DateTime.Now < deadline)
            {
                if (WindowTools.IsForegroundWindow("Confirm")) { Automation.Send("!y"); WindowTools.Wait(300); continue; }
                if (WindowTools.IsForegroundWindow("Client Error")) { Automation.Send("!o"); WindowTools.Wait(300); continue; }
                if (WindowTools.GetWindowText(WindowTools.GetForegroundWindow()).Contains("New Cases")) return;
                WindowTools.Wait(100);
            }
            StatusLog.WriteInformationEntry("CaseNumber - could not confirm the current case cleared; continuing");
        }

        /// <summary>
        /// Waits for Case Information to show the requested case. A PowerPath prompt that comes up on the way gets
        /// Alt+Y, as before, but only when one is actually showing; an error prompt stops the command.
        /// </summary>
        string WaitForCaseLoaded(string caseNumber, IntPtr mainWindow)
        {
            int powerPathProcess = WindowTools.GetWindowProcessID(mainWindow);
            int promptsAnswered = 0;
            DateTime deadline = DateTime.Now.AddSeconds(10);

            while (DateTime.Now < deadline)
            {
                string loaded = LoadedCase(mainWindow);
                if (SameCase(loaded, caseNumber)) return loaded;

                IntPtr foreground = WindowTools.GetForegroundWindow();
                if (foreground != mainWindow && WindowTools.GetWindowProcessID(foreground) == powerPathProcess)
                {
                    string prompt = WindowTools.GetWindowText(foreground);
                    if (prompt.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        StatusLog.WriteErrorEntry("CaseNumber - PowerPath error opening " + caseNumber + ": " + prompt);
                        ShowWarning(string.Format("PowerPath reported an error opening case {0}.", caseNumber));
                        return "";
                    }
                    if (promptsAnswered < 2)
                    {
                        Automation.Send("!y", 300);    // answer YES to the dialog box
                        promptsAnswered++;
                        continue;
                    }
                }
                WindowTools.Wait(100);
            }

            StatusLog.WriteErrorEntry("CaseNumber - case " + caseNumber + " did not open in PowerPath");
            ShowWarning(string.Format("Case {0} did not open in PowerPath.\n\nPlease check the case number and try again.", caseNumber));
            return "";
        }

        /// <summary>
        /// Case number showing in Case Information ("Case Information - S-26-00026"): from the main window title when
        /// the case window is maximized, else from the only Case Information child window. "" if none or ambiguous.
        /// </summary>
        string LoadedCase(IntPtr mainWindow)
        {
            string pattern = "Case Information - " + _PowerPath.caseNumberPattern;
            Match match = Regex.Match(WindowTools.GetWindowText(mainWindow), pattern);
            if (match.Success) return match.Groups[1].Value;

            List<string> cases = WindowTools.EnumChildWindows(mainWindow)
                .Select(child => Regex.Match(WindowTools.GetWindowText(child), pattern))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            return cases.Count == 1 ? cases[0] : "";
        }

        void SetCaseState(string caseNumber)
        {
            Application.SetStateProperty("CaseNumber", caseNumber);
            Match prefix = Regex.Match(caseNumber, @"^([A-Za-z]{1,3})-");
            Application.SetStateProperty("CaseType", prefix.Success ? prefix.Groups[1].Value : caseNumber.Substring(0, 1));
        }

        /// <summary>
        /// Compares prefix, year and counter numerically, so "S-26-26" and "S-26-00026" are the same case.
        /// </summary>
        static bool SameCase(string a, string b)
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

        void ShowWarning(string message)
        {
            SpeechDialogSettings dialog = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, "VoiceOver PRO - PowerPath", message);
            dialog.AddChoice("OK", "Press OK to close this dialog");
            Application.ShowSpeechDialog(dialog);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE

