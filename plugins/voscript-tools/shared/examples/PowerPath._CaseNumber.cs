#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter.PowerPath
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
  [ExtensionCommandClass(HelpText = "Formatting for PowerPath case numbers: <CaseType>-<YY>-<counter>, e.g. S-26-00026.")]
  [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of the case number counter part.")]
  [ExtensionStringProperty("CaseTypeList", DefaultValue = "CaseType", HelpText = "Name of the case type named list.")]
  [ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Determines if the case number is constructed with or without leading zeros.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _CaseNumber : ExtensionScript, ISpeechFormatter
    {
        // PowerPath's own accession formatter, as every IMS namespace carries its own - a format change here is scoped to
        // PowerPath. Replaces VOScript.Starter.DefaultCaseNumber for the PowerPath commands; same output (S-26-00026).

        public string FormatParameterizedSpeech(SpeechParameters args)
        {
            int counterLength = Property("CounterLength", 5);
            string caseTypeList = Property("CaseTypeList", "CaseType");

            // Read the property name back EXACTLY as declared above. DefaultCaseNumber read "LeadingZeroes" against a
            // declared "LeadingZeros", so the palette setting never reached it.
            bool leadingZeros = Property("LeadingZeros", true);

            return FormatParameterizedSpeech(args, counterLength, caseTypeList, leadingZeros);
        }

        public static string FormatParameterizedSpeech(SpeechParameters args, int counterLength, string caseTypeList, bool leadingZeros)
        {
            string caseType = "SUR";
            string year = DateTime.Now.Year.ToString().Substring(2);
            StringBuilder shortNumBuilder = new StringBuilder();

            if (args.Names.Count == args.Values.Count)
            {
                for (int index = 0; index < args.Names.Count; index++)
                {
                    if (args.Names[index] == caseTypeList) caseType = args.Translate(caseTypeList, args.Values[index]);
                    else if (args.Names[index] == "Year") year = args.Translate("Year", args.Values[index]);
                    else if (args.Names[index] == "Digit") shortNumBuilder.Append(args.Translate("Digit", args.Values[index]));
                }
            }

            string shortNum = shortNumBuilder.ToString();

            // PadLeft rather than a hand-built pad: DefaultCaseNumber's new string('0', counterLength - shortNum.Length)
            // throws the moment more digits are spoken than CounterLength.
            if (shortNum.Length > counterLength)
                StatusLog.WriteWarningEntry(string.Format("Spoke {0} digits but CounterLength is {1}; using the spoken digits as-is.", shortNum.Length, counterLength));

            string counter = leadingZeros ? shortNum.PadLeft(counterLength, '0') : shortNum;
            return caseType + "-" + year + "-" + counter;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE

