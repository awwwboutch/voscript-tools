#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _{{System}} : ExtensionScript
    {
        // Shared helpers for every {{System}} command. Anything more than one script needs
        // belongs here rather than being copied between commands.

        public const string BrowserClassName = "Chrome_WidgetWin_1";

        /// <summary>
        /// Window title fragments that identify a {{Vendor}} window.
        /// TODO: confirm against the live application. Include the worklist shell and any
        /// surface that opens in its own window (viewer, report).
        /// </summary>
        public static readonly List<string> WindowTitleFragments = new List<string>()
        {
            "{{WindowTitle}}"
        };

        /// <summary>
        /// Title pattern for a window showing an OPEN CASE. Scripts that act on a case rather
        /// than on the worklist need this, because the title carries the accession number by
        /// then and the fixed-fragment lookup no longer matches.
        /// TODO: confirm against the customer's real accession formats. The default covers both
        /// S26-12345 and XY-26-123456.
        /// </summary>
        public const string CaseNumberTitlePattern = @"^[A-Z]{1,3}-?[0-9]{2}-[0-9]{1,6}";

        /// <summary>
        /// Returns the title of the current {{Vendor}} browser window, or empty if none is open.
        /// Use this for worklist-level commands.
        /// </summary>
        public static string FindCurrentTitlePage(IApplicationControl Application)
        {
            List<IntPtr> allBrowserWindows = WindowTools.Instance.FindWindows(BrowserClassName, "");

            foreach (IntPtr win in allBrowserWindows)
            {
                string windowTitle = WindowTools.Instance.GetWindowText(win);

                if (string.IsNullOrWhiteSpace(windowTitle))
                    continue;

                if (WindowTitleFragments.Any(fragment => windowTitle.Contains(fragment)))
                    return windowTitle;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the actual title of the first browser window matching the pattern.
        /// </summary>
        public static string FindCurrentTitlePageByRegex(IApplicationControl Application, string titlePattern, RegexOptions options = RegexOptions.IgnoreCase)
        {
            Regex regex = new Regex(titlePattern, options);

            List<IntPtr> allBrowserWindows = WindowTools.Instance.FindWindows(BrowserClassName, "");

            foreach (IntPtr win in allBrowserWindows)
            {
                string windowTitle = WindowTools.Instance.GetWindowText(win);

                if (!string.IsNullOrEmpty(windowTitle) && regex.IsMatch(windowTitle))
                    return windowTitle;
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves the window for an open case, falling back to the worklist window. Throws a
        /// message the user can act on rather than letting the command fail later with a
        /// confusing "element not found".
        /// </summary>
        public static string RequireCaseWindow(IApplicationControl Application)
        {
            string titlePage = FindCurrentTitlePageByRegex(Application, CaseNumberTitlePattern);

            if (string.IsNullOrEmpty(titlePage))
                titlePage = FindCurrentTitlePage(Application);

            if (string.IsNullOrEmpty(titlePage))
                throw new ClientException("No {{Vendor}} window was found. Open the case in {{Vendor}} first.");

            return titlePage;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
