#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UIAutomationClient;           // For BrowserController
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class NavigateCases : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will use the next or previous case button found in the {{Vendor}} slide viewer.
            //
            // *****

            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            try
            {
                // TODO: confirm the two control names and their ControlType.
                //
                // Toolbar controls in these viewers are usually a group whose visible caption is
                // a child Text element. The Text is what carries the name, but the group is what
                // takes the click - hence FindElementOnPage(..., Text) followed by GetParent.
                // If the button itself is named, drop the GetParent and search for Button.
                IUIAutomationElement caseButton = SpeechParams.TranslateSingle(SpeechParams.Names[0]).ToLower() == "next"
                    ? _Browser.FindElementOnPage(manager, "Next Case", UIAControlType.Text)
                    : _Browser.FindElementOnPage(manager, "Previous Case", UIAControlType.Text);

                if (caseButton == null)
                    throw new ClientException("Could not find the case navigation control. This may be the first or last case in the list.");

                controller.Click(_Browser.GetParent(caseButton));
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
            }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
