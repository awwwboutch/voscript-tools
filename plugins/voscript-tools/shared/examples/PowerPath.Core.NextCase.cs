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
  [ExtensionCommandClass(HelpText = "Save the case, then either Close or Clear the case from Case Information Window")]
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
        /// Returns false when the case was left as it is (Report Builder, the Word report or Case Status still open).
        /// </summary>
        public static bool Execute(IApplicationControl Application, ISpeechBoxState SpeechBox)
        {
            WindowTools WindowTools = WindowTools.Instance;
            // This will save the case the either Close or Clear the case from Case Information Window.
            // - "New Case" or "Next Case" is used when working in the Gross Room from the Case Information Window
            // - "Close Case" is used by a Pathologist who is working from a worklist in Case Explorer. Closing the case will show their worklist.

            if (SpeechBox.ActiveDocument != null)
            {
                StatusLog.WriteInformationEntry("NextCase - Report Builder is still open; send the report first");
                return false;
            }
            
            //After a save the Word report and Case Status close in their own time. Wait for them rather than giving up the
            //moment either is still on screen - that made CaseComplete skip this step intermittently.
            DateTime deadline = DateTime.Now.AddSeconds(8);
            while ((WindowTools.FindWindow(_PowerPath.WordTitlePattern) != IntPtr.Zero || WindowTools.FindWindow("^Case Status") != IntPtr.Zero)
                   && DateTime.Now < deadline)
                WindowTools.Wait(100);
            
            if (WindowTools.FindWindow(_PowerPath.WordTitlePattern) != IntPtr.Zero || WindowTools.FindWindow("^Case Status") != IntPtr.Zero)
            {
                StatusLog.WriteInformationEntry("NextCase - the Word report or Case Status is still open; case left as is");
                return false;
            }
            
            _PowerPath.FocusPowerPath();
            WindowTools.Wait(100);
            _PowerPath.SaveCase();
            WindowTools.Wait(1000);
            _PowerPath.ClearCase();
            return true;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
