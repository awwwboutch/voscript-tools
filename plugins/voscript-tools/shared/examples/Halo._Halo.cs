// SOURCE: VOScript.Starter.Halo._Halo@3P-3481-12.xml
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
using VOScript.Starter.Browser.Manager;
using UIAutomationClient;
using System.Text.RegularExpressions;
#endregion USING NAMESPACES

namespace VOScript.Starter.Halo
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _Halo : ExtensionScript
    {
        /// <summary>
        /// This will check and return the current title for the web page to use
        /// </summary>
        /// <returns></returns>
        public static string FindCurrentTitlePage(IApplicationControl Application)
        {
            List<string> possibleWindowTitles = new List<string>()
            {
                "Cases"
            };
            
            string className = "Chrome_WidgetWin_1";
            List<IntPtr> allBrowserWindows = WindowTools.Instance.FindWindows(className,"");
            foreach(string title in possibleWindowTitles)
            {

                IntPtr window = allBrowserWindows.FirstOrDefault(win=> WindowTools.Instance.GetWindowText(win).Contains(title));
                if( window!= IntPtr.Zero)
                    return title;
            }
            return string.Empty;
        }

        public static string FindCurrentTitlePageByRegex(IApplicationControl Application, string titlePattern, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string className = "Chrome_WidgetWin_1";

            Regex regex = new Regex(titlePattern, options);

            List<IntPtr> allBrowserWindows = WindowTools.Instance.FindWindows(className, "");

            foreach (IntPtr win in allBrowserWindows)
            {
                string windowTitle = WindowTools.Instance.GetWindowText(win);

                if (!string.IsNullOrEmpty(windowTitle) && regex.IsMatch(windowTitle))
                {
                    StatusLog.WriteWarningEntry(windowTitle);
                    return windowTitle; // return the actual matched title
                }
            }

            return string.Empty;
        }
        
        public static string ConfirmCaseNumberInEpic(IApplicationControl Application)
        {
            string windowCaption = FindCurrentTitlePageByRegex(Application, @"^[A-Z]{1,2}-[0-9]{2}-[0-9]{1,6}");
            Match match = Regex.Match(windowCaption, @"^([A-Z]{1,2}-[0-9]{2}-[0-9]{1,6})");
            string haloCaseNumber = match.Groups[1].Value;
            
            string titlePage = EpicMock._EpicMock.FindCurrentTitlePage();
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();
            
            var receivedElement = _Browser.FindElementOnPage(manager, "RECEIVED", UIAControlType.Text);
            var caseNumberElement = _Browser.GetNextSiblingElement(receivedElement, 2);
            string epicCaseNumber = caseNumberElement.CurrentName;
            
            if (haloCaseNumber != epicCaseNumber)
            {
                SpeechDialogSettings mismatch = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Question,
                    "Case Mismatch Warning",
                    $"The case open in Halo ({haloCaseNumber}) does not match the case open in Epic ({epicCaseNumber}.\n\nOpen the matching case in Epic?");
                int open = mismatch.AddChoice("Yes", "");
                mismatch.AddChoice("No", "");
                
                if (Application.ShowSpeechDialog(mismatch) == open)
                {
                    return "Open";
                }
            }
            return "Ignore";
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE







