// SOURCE: VOScript.Starter.AISight._CaseNumber@4P-3040-1.xml
// Published reference example, extracted 2026-09-01 from demo\sales.
// Read-only: do not edit. See examples/README.md for what each one demonstrates.

#region USING NAMESPACES
using System;
using System.Text;
using VoiceOver.Extensions;
#endregion USING NAMESPACES

namespace VOScript.Starter.AISight
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
  [ExtensionExtensionClass(HelpText = "Formatting for case numbers.  Expects 'dash' to be dictated if year included.")]
  [ExtensionIntProperty("CounterLength", DefaultValue = 5, HelpText = "Length (in characters) of case number counter part.")]
  [ExtensionStringProperty("CaseTypeList", DefaultValue = "CaseType", HelpText = "String name the case type named list.")]
  [ExtensionBoolProperty("LeadingZeros", DefaultValue = true, HelpText = "Determines if case number is constructed with or without leading zeros.")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _CaseNumber : ExtensionScript, ISpeechFormatter
    {
        public string FormatParameterizedSpeech(SpeechParameters args)
        {
            int counterLength = Property("CounterLength", 5);
            string caseTypeList = Property("CaseTypeList", "AISightCaseType");
            bool leadingZeros = Property("LeadingZeroes", true);
            return FormatParameterizedSpeech(args, counterLength, caseTypeList, leadingZeros);
        }

        public static string FormatParameterizedSpeech(SpeechParameters args, int counterLength, string caseTypeList, bool leadingZeros)
        {
            string caseType = "";
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

            string fullNum = new string('0', counterLength - shortNum.Length) + shortNum;
            string caseNumber;
            if (leadingZeros) caseNumber = caseType + "-" + "23" + "-" + fullNum;
            else caseNumber = caseType + "-" + "23" + "-" + shortNum;

            return caseNumber;
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE


