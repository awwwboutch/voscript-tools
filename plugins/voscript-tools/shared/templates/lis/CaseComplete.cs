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
    [ExtensionCommandClass(HelpText = "Sends the report, saves it to {{Vendor}}, then moves to the next case. Stops at any step that does not finish. Trigger: case complete")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class CaseComplete : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // One command for the end of a case: each step is a *_Logic class returning true only when it finished, and the
            // chain stops at the first one that didn't - a failed transfer never ends with the case cleared underneath the
            // pathologist. A sign-out step backed out of by the save counts as finished.
            //
            // *****

            if (SpeechBox.ActiveDocument != null)
            {
//@if Word
                if (!ReturnToWord_Logic.Execute(Application, SpeechBox, DocumentStore)) return;
                WindowTools.Wait(500);  //let Report Builder finish closing; the next step stops if it still looks open
//@else
                if (!{{ReturnToName}}_Logic.Execute(Application, SpeechBox, DocumentStore)) return;
                WindowTools.Wait(500);  //let Report Builder finish closing; NextCase stops if it still looks open
//@endif
            }
//@if SaveToLis
            if (!{{ReturnToName}}_Logic.Execute(Application, SpeechBox, SpeechParams)) return;
            WindowTools.Wait(500);
//@endif
            NextCase_Logic.Execute(Application, SpeechBox);

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
