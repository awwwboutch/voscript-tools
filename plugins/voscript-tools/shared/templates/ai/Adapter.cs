#region USING NAMESPACES
using System;
using System.Collections.Generic;
using UIAutomationClient;           // For BrowserController
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using VOScript.Starter.CapResultParser;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _{{System}}Adapter : ExtensionScript
    {
        // The ExtensionScript wrapper above exists so VO_ScriptEdit accepts the file. The real
        // adapter is the class below, which is what PullBiomarkerResults constructs.
    }

    /// <summary>
    /// Reads AI result values out of the {{Vendor}} UI. One method, called once per label the
    /// CAP mapping asks for.
    /// </summary>
    public class {{System}}Adapter : IAiResultSource
    {
        readonly Browser.Manager.BrowserManager _manager;

        // Scraping the whole results panel once and serving lookups from a cache is much faster
        // than walking the tree per label, and it keeps the log to a single line per case.
        Dictionary<string, string> _cache;

        public {{System}}Adapter(Browser.Manager.BrowserManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Returns the raw string value for the given on-screen label, or null when the label
        /// is not present. A missing label is expected - log a warning, never throw.
        /// </summary>
        public string ReadValueByLabel(string label)
        {
            try
            {
                if (_cache == null) _cache = ScrapeResultsPanel();

                string value;
                return _cache.TryGetValue(label, out value) ? value : null;
            }
            catch (Exception ex)
            {
                StatusLog.WriteWarningEntry($"[{{System}}Adapter] Could not read \"{label}\": {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Walks the {{Vendor}} AI results panel once and returns every label/value pair it finds.
        /// </summary>
        Dictionary<string, string> ScrapeResultsPanel()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // TODO: implement against the live {{Vendor}} results panel.
            //
            // Start with _Browser.DumpTree on the panel root to see the real structure, then
            // pick the most stable addressing available, in this order of preference:
            //   1. AutomationId, scoped to the panel root (_Browser.FindByPath).
            //   2. A label element with the value as its next sibling.
            //   3. Row/column index - last resort, breaks whenever the vendor reorders.
            //
            // Log one summary line when done, e.g.
            // StatusLog.WriteInformationEntry($"[{{System}}Adapter] slide tray: {values.Count} values scraped.");

            return values;
        }
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE
