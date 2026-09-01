#region USING NAMESPACES
using System;
using System.Collections.Generic;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using {{Namespace}}.Labels;
#endregion USING NAMESPACES

namespace {{Namespace}}
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _{{System}}LabelRegistry : ExtensionScript
    {
        // Maps a CAP template's PartKey to the {{System}} label overlay for that template.
        //
        // The key MUST be the value of the CAP template XML's own hidden "filename" field, not
        // the local export filename. Those two conventions differ, and using the wrong one
        // produces a silent "no overlay registered" every time. Read the field off the template
        // XML and paste it here verbatim.
        static readonly Dictionary<string, Func<Dictionary<string, string>>> _registry =
            new Dictionary<string, Func<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase)
            {
                // TODO: register each supported template, e.g.
                // ["Breast.Bmk.169_1.010.001.REL_sdcFDF.xml"] = _{{System}}BreastBmk169Labels.Build,
            };

        public static Dictionary<string, string> GetLabelsForPartKey(string partKey)
        {
            if (string.IsNullOrEmpty(partKey)) return null;

            Func<Dictionary<string, string>> builder;

            if (!_registry.TryGetValue(partKey, out builder))
            {
                StatusLog.WriteWarningEntry(
                    "[{{System}}LabelRegistry] No label overlay registered for PartKey '" + partKey + "'. " +
                    "If {{System}} is meant to support this template, add a _{{System}}<Template>Labels.cs " +
                    "and an entry in _{{System}}LabelRegistry.cs.");
                return null;
            }

            return builder();
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
