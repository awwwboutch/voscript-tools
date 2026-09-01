#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionExtensionClass(HelpText = "Formatting for {{Vendor}} case numbers. Expects 'dash' to be dictated if the year is included.")]
    [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of the case number counter part.")]
    [ExtensionStringProperty("CaseTypeList", DefaultValue = "{{CaseTypeList}}", HelpText = "Name of the case type named list.")]
    [ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Determines if the case number is constructed with or without leading zeros.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _CaseNumber : ExtensionScript, ISpeechFormatter
    {
        // Every vendor namespace carries its own _CaseNumber, so a format change here is scoped
        // to {{System}} and cannot disturb the other integrations.
        //
        // TODO: confirm the assembled format against real {{Vendor}} accession numbers. The
        // default below builds <CaseType><YY>-<counter>, e.g. SN26-00123. Systems that separate
        // the case type from the year (SN-26-00123) need the extra dash added below.

        public string FormatParameterizedSpeech(SpeechParameters args)
        {
            int counterLength = Property("CounterLength", 5);
            string caseTypeList = Property("CaseTypeList", "{{CaseTypeList}}");

            // Read the property name back EXACTLY as declared above. A typo here silently
            // returns the default forever - this shipped in more than one namespace before.
            bool leadingZeros = Property("LeadingZeros", true);

            return FormatParameterizedSpeech(args, counterLength, caseTypeList, leadingZeros);
        }

        public static string FormatParameterizedSpeech(SpeechParameters args, int counterLength, string caseTypeList, bool leadingZeros)
        {
            string caseType = "SN";
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

            // PadLeft rather than building the pad by hand: the hand-built version throws
            // ArgumentOutOfRangeException the moment a pathologist speaks more digits than
            // CounterLength, which crashes the command instead of degrading.
            string fullNum = leadingZeros ? shortNum.PadLeft(counterLength, '0') : shortNum;

            if (shortNum.Length > counterLength)
            {
                StatusLog.WriteWarningEntry(
                    $"Spoke {shortNum.Length} digits but CounterLength is {counterLength}. " +
                    "Using the spoken digits as-is; check the CounterLength property on the command.");
            }

            return caseType + year + "-" + fullNum;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
