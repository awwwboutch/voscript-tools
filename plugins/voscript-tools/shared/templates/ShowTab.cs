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
    public class ShowTab : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // This will bring the spoken tab into focus in {{Vendor}}, however {{Vendor}} happens
            // to expose that tab.
            //
            // *****

            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            string tabName = SpeechParams.TranslateSingle(SpeechParams.Names[0]);

            // Real tabs expose TabItem and are focused; toolbar toggles are Buttons and are
            // clicked. Systems mix the two in one tray, so decide per name. List the entries
            // that really are TabItems here and let everything else fall through to Button.
            UIAControlType type;

            switch (tabName)
            {
                // TODO: list this system's actual TabItem tabs, e.g.
                // case "Comments":
                // case "Report":
                // case "AI Results":
                //     type = UIAControlType.TabItem;
                //     break;
                default:
                    type = UIAControlType.Button;
                    break;
            }

            try
            {
                IUIAutomationElement tab = _Browser.FindElementOnPage(manager, tabName, type);

                if (tab == null)
                    throw new ClientException($"Could not find the \"{tabName}\" tab in {{Vendor}}.");

                if (type == UIAControlType.TabItem) _Browser.FocusTabItem(tab);
                else                                controller.Click(tab);
            }
            catch (ClientException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry($"Error controlling {{Vendor}}: {ex.Message}", ex);
            }

            // ---------------------------------------------------------------------------
            // TODO: pick the variant that matches {{Vendor}} and delete the rest.
            //
            // The block above is the general case. Two other shapes are in production:
            //
            // 1. THE TAB IS ONLY PRESENT WHEN ITS PANEL IS OPEN (BXLink).
            //    FindElementOnPage returns null because the whole panel is collapsed, not
            //    because the tab does not exist. Open the panel, then focus the tab. Check
            //    IsTabSelected first so an already-selected tab is not toggled off.
            //
            //    IUIAutomationElement tab = _Browser.FindElementOnPage(manager, tabName, UIAControlType.TabItem);
            //    if (tab == null)
            //    {
            //        IUIAutomationElement document = controller.FindWebPageDocument(titlePage);
            //        IUIAutomationElement firstChild = _Browser.GetNthChild(document, 1);
            //        IUIAutomationElement grandchild = _Browser.GetNthChild(firstChild, 1);
            //        IUIAutomationElement panelBar = _Browser.GetNextSiblingElement(grandchild, 2);
            //        IUIAutomationElement opener = _Browser.GetNthChildByType(panelBar, UIAControlType.Hyperlink, 1);
            //        controller.Click(opener);
            //        WindowTools.Wait(500);
            //        tab = _Browser.FindElementOnPage(manager, tabName, UIAControlType.TabItem);
            //    }
            //    if (!_Browser.IsTabSelected(tab))
            //    {
            //        _Browser.FocusTabItem(tab);
            //        WindowTools.Wait(500);
            //    }
            //
            // 2. THE TAB TOGGLES HAVE NO USABLE ACCESSIBLE NAMES (Halo).
            //    The icon buttons in the tool tray cannot be found by name at all. Anchor off
            //    one button that IS named, and have the named list translate the spoken tab to
            //    a sibling OFFSET from that anchor rather than to a name. Adding a tab then
            //    means editing the named list, not the script.
            //
            //    IUIAutomationElement anchor = _Browser.FindElementOnPage(manager, "Viewer information (F1)", UIAControlType.Button);
            //    int offset = int.Parse(SpeechParams.TranslateSingle(SpeechParams.Names[0]));
            //    IUIAutomationElement toolbarButtonGroup = _Browser.GetPreviousSiblingElement(anchor, offset);
            //    IUIAutomationElement toolbarButton = _Browser.GetChild(toolbarButtonGroup);
            //    controller.Click(toolbarButton);
            //
            //    Pick an anchor whose accessible name includes its hotkey - those are the names
            //    the vendor sets deliberately and is least likely to churn.
            // ---------------------------------------------------------------------------

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
