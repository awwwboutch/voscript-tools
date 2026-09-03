// SOURCE: VOScript.Starter.Fusion.ShowTab@1P-3003-3.xml
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

namespace VOScript.Starter.Fusion
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
            // 
            //
            // *****

            string titlePage = _Fusion.FindCurrentTitlePageByRegex(Application, @"Case [A-Z]{2}-[0-9]{2}-[0-9]{1,5}");
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            var controller = manager.GetBrowserController();
            IUIAutomationElement document = controller.FindWebPageDocument(titlePage);

            string tabName = SpeechParams.TranslateSingle(SpeechParams.Names[0]);
            UIAControlType type;
            
            switch (tabName)
            {
                case "Comments":
                case "Report":
                case "Requests":
                case "Quality":
                case "AI Resuls":
                    type = UIAControlType.TabItem;
                    break;
                default:
                    type = UIAControlType.Button;
                    break;
            }

            try
            {
                //Verifying that the Report panel is open and, if not, opening it.
                IUIAutomationElement tab = _Browser.FindElementOnPage(manager, tabName, type);
                string detail = tab.CurrentControlType.ToString();
                if (type == UIAControlType.TabItem)
                    _Browser.FocusTabItem(tab);
                else
                    controller.Click(tab);
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling Fusion: {ex.Message}", ex);
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





