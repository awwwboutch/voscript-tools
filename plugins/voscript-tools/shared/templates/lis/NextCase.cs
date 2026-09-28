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
    [ExtensionCommandClass(HelpText = "Saves the current {{Vendor}} case and clears it, ready for the next one. Trigger: next case")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class NextCase : CommandScript
    {
        public override void Execute()
        {
            NextCase_Logic.Execute(Application, SpeechBox);
        }
    }

    public class NextCase_Logic : ExtensionScript
    {
        /// <summary>
        /// Returns false when the case was left as it is (Report Builder still open, or the report still on screen).
        /// Every early exit logs why - a silent return here is what made CaseComplete look intermittent in PowerPath.
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox)
        {
            // Author: {{Author}}
            // Date: {{Date}}

            if (SpeechBox.ActiveDocument != null)
            {
                StatusLog.WriteInformationEntry("NextCase - Report Builder is still open; send the report first");
                return false;
            }
//@if SaveToLis

            //After the save the Word report closes in its own time. Wait for it rather than giving up the moment it is still
            //on screen.
            DateTime deadline = DateTime.Now.AddSeconds(8);
            while (_{{System}}.FindWordReport() != IntPtr.Zero && DateTime.Now < deadline)
                WindowTools.Instance.Wait(100);
            if (_{{System}}.FindWordReport() != IntPtr.Zero)
            {
                StatusLog.WriteInformationEntry("NextCase - the Word report is still open; case left as is");
                return false;
            }
//@endif

            return _{{System}}.SaveAndClearCase(Application);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
