#region USING NAMESPACES
using System;
using System.Collections.Generic;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{Namespace}}.Labels
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _{{System}}BreastBmk169Labels : ExtensionScript
    {
        // Label overlay for CAP Breast Bmk 169 as {{Vendor}} renders it.
        //
        // Key   = the vendor-agnostic biomarker key the CAP mapping asks for.
        // Value = the exact on-screen label {{Vendor}} puts next to that value, which is what
        //         _{{System}}Adapter.ReadValueByLabel is handed.
        //
        // A missing key here does NOT look like a missing key at runtime. The engine logs
        // "No values scraped ... skipping biomarker" for that marker, which reads like a
        // scraping failure. If the adapter's own log line shows it scraped the slide tray fine
        // but one biomarker is skipped, the entry below is what is missing.
        public static Dictionary<string, string> Build()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // TODO: fill in from the live {{Vendor}} results panel. Example shape:
                // ["ER.PositivityPercent"]      = "ER % Positive",
                // ["ER.Intensity"]              = "ER Intensity",
                // ["PR.PositivityPercent"]      = "PR % Positive",
                // ["PR.Intensity"]              = "PR Intensity",
                // ["HER2.Score"]                = "HER2 Score",
                // ["Ki-67.ProliferationIndex"]  = "Ki-67 Index",
            };
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
