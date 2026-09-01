#region USING NAMESPACES
using System;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using VOScript.Starter.CapResultParser;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Pulls AI biomarker results from {{Vendor}} into the active CAP checklist. Standard trigger: pull biomarker results")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class PullBiomarkerResults : CommandScript
    {
        public override void Execute()
        {
            // Author: {{Author}}
            // Date: {{Date}}
            // Use:
            // Entry point for the CAP biomarker pipeline. This script only wires things up -
            // the scraping lives in _{{System}}Adapter, the CAP field mapping is vendor-agnostic
            // and shared, and the vendor's on-screen labels live in the Labels namespace.
            //
            // *****

            // 1. Find the {{Vendor}} window. This runs against an OPEN CASE, so the worklist
            //    title lookup will not match - use the regex form.
            string titlePage = _{{System}}.FindCurrentTitlePageByRegex(Application, _{{System}}.CaseNumberTitlePattern);

            if (string.IsNullOrEmpty(titlePage))
            {
                StatusLog.WriteWarningEntry(
                    "[{{System}}.PullBiomarkerResults] {{Vendor}} window not found. Open the case in {{Vendor}} first.");
                return;
            }

            // 2. Detect which CAP template is loaded in Report Builder.
            string partKey = _TemplateDetector.GetActivePartKey(SpeechBox);
            if (string.IsNullOrEmpty(partKey)) return;

            // 3. Resolve the shared CAP mapping and this vendor's label overlay.
            var mapping = _TemplateDetector.GetMappingForPartKey(partKey);
            var labels = _{{System}}LabelRegistry.GetLabelsForPartKey(partKey);
            if (mapping == null || labels == null) return;

            // 4. Build the adapter and run the engine.
            using var manager = new Browser.Manager.BrowserManager(titlePage);
            using var controller = manager.GetBrowserController();

            var adapter = new {{System}}Adapter(manager);
            _CapEngine.Run(adapter, SpeechBox, mapping, labels);

        } // close Execute

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
