// PREREQUISITE: VOScript.Starter.CapResultParser._TemplateDetector
// Source revision 3P-3471-6, exported 2026-09-01 from demo\sales.
// Tier: ai
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using VOScript.Starter.CapResultParser.Mappings;
using VOScript.Standard.SpeechBox;
#endregion USING NAMESPACES

namespace VOScript.Starter.CapResultParser
{
    #region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    #endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

    /// <summary>
    /// Identifies which CAP template is loaded in the active VoiceOver document
    /// and returns the matching biomarker mapping.
    ///
    /// Adding a new CAP template = one entry in the _registry below, plus a
    /// matching ExtensionScript in CapResultParser.Mappings whose Build()
    /// returns the dictionary.
    ///
    /// Each vendor also has its own per-vendor label registry (e.g.
    /// _HarnessLabelRegistry) that the vendor's entry point queries with the
    /// PartKey returned by GetActivePartKey to get its label overlay.
    /// </summary>
    // CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _TemplateDetector : ExtensionScript
    {
        // PartKey (from IDocListItem) -> mapping factory.
        static readonly Dictionary<string, Func<Dictionary<string, BiomarkerMap>>> _registry =
            new Dictionary<string, Func<Dictionary<string, BiomarkerMap>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Breast.Bmk.169_1.010.001.REL_sdcFDF.xml"] = _BreastBmk169Mapping.Build,
                ["Lung.Bmk.227_2.002.001.REL_sdcFDF.xml"] = _LungBmk227Mapping.Build,
                
                // Future entries (one line each) — key is the CAP template's own
                // hidden "filename" field value, NOT the local export filename:
                // ["Gynecologic.Bmk.567_1.003.001.REL_sdcFDF.xml"] = _GynecologicBmk567Mapping.Build,
            };

        /// <summary>
        /// Returns the PartKey of the CAP checklist currently loaded in the
        /// active VoiceOver document, or null if no checklist is loaded.
        /// Vendor entry points use this to look up both the CAP mapping
        /// (via GetMappingForPartKey) and their per-vendor label overlay
        /// (via _<Vendor>LabelRegistry.GetLabelsForPartKey).
        /// </summary>
        public static string GetActivePartKey(ISpeechBoxState speechBox)
        {
            IDocListItem checklist = null;
            try
            {
                checklist = ListTools.FindTopListItem(speechBox.InputFocus) as IDocListItem;
            }
            catch (Exception ex)
            {
                StatusLog.WriteWarningEntry("[TemplateDetector] FindTopListItem threw: " + ex.Message);
                return null;
            }

            if (checklist == null)
            {
                StatusLog.WriteWarningEntry(
                    "[TemplateDetector] Active document does not appear to contain a CAP checklist " +
                    "(FindTopListItem returned null or non-IDocListItem). Make sure a CAP biomarker " +
                    "report is loaded in Report Builder before running this script.");
                return null;
            }

            var filenameField = checklist.FindField("filename");
            if (filenameField == null)
            {
                StatusLog.WriteWarningEntry(
                    "[TemplateDetector] Active checklist has no 'filename' field. Make sure " +
                    "VoiceOver's input focus is actually on the CAP checklist (not the vendor's " +
                    "browser window or a different document) before running this script.");
                return null;
            }

            string partKey = filenameField.Value;
            if (string.IsNullOrEmpty(partKey))
            {
                StatusLog.WriteWarningEntry("[TemplateDetector] Checklist PartKey is null/empty.");
                return null;
            }

            return partKey;
        }

        /// <summary>
        /// Returns the biomarker mapping for the given PartKey, or null if no
        /// mapping is registered. Logs a warning in the null case.
        /// </summary>
        public static Dictionary<string, BiomarkerMap> GetMappingForPartKey(string partKey)
        {
            if (string.IsNullOrEmpty(partKey)) return null;
            Func<Dictionary<string, BiomarkerMap>> builder;
            if (!_registry.TryGetValue(partKey, out builder))
            {
                StatusLog.WriteWarningEntry(
                    "[TemplateDetector] No mapping registered for PartKey '" + partKey + "'. " +
                    "If this is a new CAP template version, add an entry to the registry in " +
                    "_TemplateDetector.cs and create a matching Mapping ExtensionScript.");
                return null;
            }
            StatusLog.WriteInformationEntry("[TemplateDetector] Active template: " + partKey);
            return builder();
        }

        /// <summary>
        /// Convenience wrapper: detects the active template and returns its
        /// CAP mapping in one call. (Kept for callers that don't need the
        /// PartKey separately. Vendor entry points typically need the PartKey
        /// to also fetch their label overlay, so they call GetActivePartKey
        /// + GetMappingForPartKey directly.)
        /// </summary>
        public static Dictionary<string, BiomarkerMap> GetMappingForActiveDocument(ISpeechBoxState speechBox)
        {
            return GetMappingForPartKey(GetActivePartKey(speechBox));
        }
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE
