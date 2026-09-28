#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter.PowerPath.Core
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseComplete : CommandScript
    {
        public override void Execute()
        {
            // Author: vbAndrew
            // Date: 8/14/2024
            // Use:
            // Transfers the report to Word, saves it back to PowerPath (status update), then moves to the next case.
            // Each step stops the chain if it didn't finish, so a failed transfer or a prompt left for the user
            // never ends with the case being closed underneath them.
            // *****
            
            if (SpeechBox.ActiveDocument != null)
            {
                if (!ReturnWord_Logic.Execute(Application, SpeechBox, DocumentStore)) return;
                WindowTools.Wait(500);  //let SpeechBox finish closing; ReturnPowerPath stops if it still looks open
            }
            if (!ReturnPowerPath_Logic.Execute(Application, SpeechBox, DocumentStore, SpeechParams)) return;
            WindowTools.Wait(500);
            NextCase_Logic.Execute(Application, SpeechBox);

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
