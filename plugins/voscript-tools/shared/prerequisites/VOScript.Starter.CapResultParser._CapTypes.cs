// PREREQUISITE: VOScript.Starter.CapResultParser._CapTypes
// Source revision 2P-3250-3, exported 2026-09-01 from demo\sales.
// Tier: ai
// Create this in the target tenant BEFORE any generated starter script will compile.
// Do not edit. See prerequisites/README.md.

#region USING NAMESPACES
using System;
using System.Collections.Generic;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter.CapResultParser
{
    #region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    #endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

    // CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _CapTypes : ExtensionScript
    {
        // Placeholder. All shared data types for the CAP result-parser engine
        // are defined as sibling classes below.
    }

    /// <summary>
    /// Holds the raw strings scraped from the AI tool for one biomarker,
    /// keyed by AiProperty name ("Status", "PositivityPercent", etc.).
    /// </summary>
    public class ScrapedValues
    {
        public string Biomarker;
        public Dictionary<string, string> Values = new Dictionary<string, string>();

        // Per-item payloads for multiPickPerItem (MMR-style):
        //   ExtraFields[biomarkerName] = { "MLH 1":"Loss", "PMS 2":"Intact", ... }
        public Dictionary<string, Dictionary<string, string>> ExtraFields
            = new Dictionary<string, Dictionary<string, string>>();

        public string Get(string aiProperty)
        {
            string v;
            return Values.TryGetValue(aiProperty, out v) ? v : null;
        }
    }

    /// <summary>
    /// One biomarker's complete CAP-side mapping spec: optional gate, plus a
    /// list of QuestionMaps describing each CAP question. Note: AI-side label
    /// strings (what shows up on a vendor's UI) live in a separate per-vendor
    /// label overlay, NOT here. See _<Vendor><Template>Labels.cs.
    /// </summary>
    public class BiomarkerMap
    {
        public List<QuestionMap> Questions { get; set; } = new List<QuestionMap>();

        // Optional: for "gated" templates (e.g. CAP Breast Bmk169), the parent
        // MultiPick "Test(s) Performed" question must be checked for this
        // biomarker before its sub-questions are accessible. If both fields are
        // set, the engine queues (GateQuestionCKey ← GateAnswerCKey) and flushes
        // it as part of the final pipe-delimited MultiPick write.
        // Leave null for ungated templates.
        public string GateQuestionCKey { get; set; }
        public string GateAnswerCKey { get; set; }
    }

    /// <summary>
    /// One CAP question's mapping. All fields here are CAP-side (vendor-
    /// agnostic): the CAP question's CKey, how to translate an AI value into
    /// a CAP answer CKey (ValueMap / RangeMap / Compute), and any gating
    /// predicate.
    ///
    /// The vendor-specific bit — what label this question's value sits next
    /// to on the AI tool's UI — lives in the per-vendor label overlay,
    /// keyed by "BiomarkerName.AiProperty" (e.g. "ER.Status").
    /// </summary>
    public class QuestionMap
    {
        public string CKey { get; set; }
        public string Label { get; set; }              // for logging only
        public string Type { get; set; }               // "categorical" | "percentageRange" | "computed" | "multiPickPerItem"
        public string AiProperty { get; set; }         // logical slot name ("Status", "PositivityPercent", "Intensity", "Score")
                                                        // — also used as the overlay key suffix
        public Func<ScrapedValues, bool> OnlyIf { get; set; }
        public Dictionary<string, string> ValueMap { get; set; }
        public List<RangeRule> RangeMap { get; set; }
        public Func<ScrapedValues, string> Compute { get; set; }
        public List<MultiPickItem> MultiPickItems { get; set; }
    }

    public class RangeRule
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public string Answer { get; set; }
    }

    /// <summary>
    /// One item in a multiPickPerItem question (e.g. one MMR protein).
    /// AI-side label is NOT here — it lives in the overlay keyed by
    /// "BiomarkerName.ItemKey" (e.g. "IHC.MLH 1").
    /// </summary>
    public class MultiPickItem
    {
        public string ItemKey { get; set; }
        public string ItemCKey { get; set; }
        public string SubQuestionCKey { get; set; }
        public Dictionary<string, string> SubValueMap { get; set; }
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE
