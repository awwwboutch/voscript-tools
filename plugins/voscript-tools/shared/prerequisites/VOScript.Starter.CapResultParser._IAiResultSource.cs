// PREREQUISITE: VOScript.Starter.CapResultParser._IAiResultSource
// Source revision 1P-3235-2, exported 2026-09-01 from demo\sales.
// Tier: ai
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter.CapResultParser
{
    #region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    #endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

    // CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _IAiResultSource : ExtensionScript
    {
        // This file exists to declare the IAiResultSource interface (below).
        // The ExtensionScript wrapper is a placeholder required by the IDE.
        // No static helpers live here — the interface is the entire payload.
    }

    /// <summary>
    /// Vendor adapter contract. Each digital pathology integration (Concentriq,
    /// Corista, AISight, Harness, etc.) implements this interface to expose its
    /// AI biomarker results to the shared CAP mapping engine.
    ///
    /// The engine has no opinion about how a vendor surfaces its data —
    /// it just asks "what's the value next to this label?" and the adapter
    /// translates that into vendor-specific UI gymnastics.
    /// </summary>
    public interface IAiResultSource
    {
        /// <summary>
        /// Returns the raw string value associated with the given page label,
        /// or null if the label isn't present (or its value can't be read).
        /// Implementations should handle their own exceptions and log warnings,
        /// not throw — a missing label is expected behavior, not an error.
        /// </summary>
        string ReadValueByLabel(string label);
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE
