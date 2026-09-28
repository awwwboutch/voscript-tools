#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{CoreNamespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Opens the spoken case in {{Vendor}} and starts dictation on it. Trigger: <{{CaseTypeList}}> [<Year> dash|] [<Digit>|1-5]")]
    [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of the case number counter part.")]
    [ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Determines if the case number is constructed with or without leading zeros.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseNumber : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will open the spoken case in {{Vendor}} and, once {{Vendor}} is really showing it, start dictation on
            // it (DictateSection). Nothing downstream runs on a case that didn't open.
            //
            // *****

            string caseNumber = _CaseNumber.FormatParameterizedSpeech(SpeechParams, Property("CounterLength", 5), "{{CaseTypeList}}", Property("LeadingZeros", true));
            if (string.IsNullOrEmpty(caseNumber)) return;

            //Report Builder open: the case number is dictated text (e.g. a referenced case). Insert it, and leave the open
            //case's state alone.
            if (SpeechBox.ActiveDocument != null)
            {
                IDocField focus = SpeechBox.InputFocus;
                if (focus == null) return;
                focus.Value = focus.Value.Trim() + " " + caseNumber;
                SpeechBox.RefreshDocumentChanges();
                SpeechBox.SetInputFocus(focus);
                Automation.Send("{RIGHT}");
                return;
            }

            _{{System}}.SetCaseState(Application, caseNumber);
//@if Word

            //A Word report still open: the same case means it's already up; another case must be saved or closed first
            IntPtr openWord = _{{System}}.FindWordReport();
            if (openWord != IntPtr.Zero)
            {
                string openCase = _{{System}}.WordCaseNumber(openWord);
                if (!_{{System}}.SameCase(openCase, caseNumber))
                {
                    _{{System}}.ShowWarning(Application, string.Format("The Word report for case {0} is still open.\n\nPlease save or close it before opening case {1}.", openCase, caseNumber));
                    return;
                }
                _{{System}}.SetCaseState(Application, openCase);
                DictateSection_Logic.Execute(Application, SpeechBox, DocumentStore, SpeechParams, true);
                return;
            }
//@endif

            string loadedCase = _{{System}}.OpenCase(Application, caseNumber);
            if (loadedCase == "")
            {
                _{{System}}.ShowWarning(Application, string.Format("Case {0} did not open in {{Vendor}}.\n\nPlease check the case number and try again.", caseNumber));
                return;
            }
            _{{System}}.SetCaseState(Application, loadedCase);   //{{Vendor}}'s own formatting, so later comparisons match

            DictateSection_Logic.Execute(Application, SpeechBox, DocumentStore, SpeechParams, true);

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
