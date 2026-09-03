// SOURCE: VOScript.Starter.BXLink.ShowTab@1P-2871-3.xml
// Published reference example, extracted 2026-09-01 from demo\sales.
// Read-only: do not edit. See examples/README.md for what each one demonstrates.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using System.Threading;
using UIAutomationClient;
#endregion USING NAMESPACES

namespace VOScript.Starter.BXLink
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class ShowTab : CommandScript
    {
        public override void Execute()
        {
            // Author: andrew.boutcher@voicebrook.com
            // Date: 12/10/2025
            // Use:
            // This will send a checklist from Report Builder into the Case Comments tab of Lumea's BXLink
            //
            // *****

            string titlePage = _BXLink.FindCurrentTitlePage(Application);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();
            IUIAutomationElement document = controller.FindWebPageDocument(titlePage);
            
            string tabName = SpeechParams.TranslateSingle(SpeechParams.Names[0]);

            try
            {
                //Verifying that the Report panel is open and, if not, opening it.
                IUIAutomationElement tab = _Browser.FindElementOnPage(manager, tabName, UIAControlType.TabItem);
                if (tab == null)
                {
                    IUIAutomationElement firstChild = _Browser.GetNthChild(document, 1);
                    IUIAutomationElement grandchild = _Browser.GetNthChild(firstChild, 1);

                    IUIAutomationElement reportBar = _Browser.GetNextSiblingElement(grandchild, 2);
                    IUIAutomationElement diagnosisTab = _Browser.GetNthChildByType(reportBar, UIAControlType.Hyperlink, 1);
                    controller.Click(diagnosisTab);
                    WindowTools.Wait(500);
                }

                tab = _Browser.FindElementOnPage(manager, tabName, UIAControlType.TabItem);
                if (!_Browser.IsTabSelected(tab))
                {
                    _Browser.FocusTabItem(tab);
                    WindowTools.Wait(500);
                }

            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling BXLink: {ex.Message}", ex);
            }
            finally
            {
                // Must dispose to avoid memory leaks and/or crashes
                controller?.Dispose();
            }

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE




