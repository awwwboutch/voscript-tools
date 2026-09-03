// PREREQUISITE: VOScript.Starter.CapResultParser._CapEngine
// Source revision 4P-3523-6, exported 2026-09-01 from demo\sales.
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
    public class _CapEngine : ExtensionScript
    {
        // Convenience entry point: vendor CommandScripts can either instantiate
        // Engine directly or call this static helper for a one-shot run.
        public static void Run(IAiResultSource source, ISpeechBoxState speechBox,
                               Dictionary<string, BiomarkerMap> biomarkerMappings,
                               Dictionary<string, string> labelOverlay)
        {
            new Engine(source, speechBox).Run(biomarkerMappings, labelOverlay);
        }
    }

    /// <summary>
    /// The shared CAP biomarker mapping engine. Takes an IAiResultSource (the
    /// vendor adapter), a CAP mapping dictionary (vendor-agnostic), and a
    /// per-vendor label overlay (the strings that appear on the vendor's UI,
    /// keyed by "BiomarkerName.AiProperty" or "BiomarkerName.ItemKey").
    ///
    /// Walks every biomarker, scrapes its values via the adapter, resolves CAP
    /// answer C-Keys, and writes them into the active VoiceOver report.
    ///
    /// Knows nothing about specific vendors or specific CAP templates.
    /// </summary>
    public class Engine
    {
        readonly IAiResultSource _source;
        readonly ISpeechBoxState _speechBox;

        // Accumulates MultiPick selections during a single Run() call.
        // See FlushMultiPicks for the why.
        Dictionary<string, HashSet<string>> _pendingMultiPicks;

        // Per-run label overlay: "BiomarkerName.AiProperty" -> AI-tool label string.
        Dictionary<string, string> _labels;

        public Engine(IAiResultSource source, ISpeechBoxState speechBox)
        {
            _source = source;
            _speechBox = speechBox;
        }

        /// <summary>
        /// Run the mapping engine: scrape every biomarker, resolve and write
        /// each question's CAP answer, flush all queued MultiPick selections.
        /// Biomarkers with no labels in the overlay are skipped (vendor declares
        /// what it supports by what it puts in the overlay).
        /// </summary>
        public void Run(Dictionary<string, BiomarkerMap> biomarkerMappings,
                Dictionary<string, string> labelOverlay)
        {
            _pendingMultiPicks = new Dictionary<string, HashSet<string>>();
            _labels = labelOverlay ?? new Dictionary<string, string>();
        
            foreach (var kvp in biomarkerMappings)
            {
                string biomarkerName = kvp.Key;
                BiomarkerMap map = kvp.Value;
        
                // STEP 1 — scrape every page value this biomarker references.
                ScrapedValues scraped = ScrapeBiomarkerValues(biomarkerName, map);
        
                if (scraped.Values.Count == 0
                    && (!scraped.ExtraFields.ContainsKey(biomarkerName)
                        || scraped.ExtraFields[biomarkerName].Count == 0))
                {
                    LogInfo("[" + biomarkerName + "] No values scraped from page; skipping biomarker.");
                    continue;
                }
        
                // STEP 1b — if this biomarker is gated, queue the gate selection.
                if (!string.IsNullOrEmpty(map.GateQuestionCKey) && !string.IsNullOrEmpty(map.GateAnswerCKey))
                {
                    QueueMultiPickAnswer(map.GateQuestionCKey, map.GateAnswerCKey);
                    LogInfo("[" + biomarkerName + "] gate queued: " + map.GateQuestionCKey +
                            " += " + map.GateAnswerCKey);
                }
        
                // STEP 2 — resolve and write each question.
                foreach (var q in map.Questions)
                {
                    if (q.OnlyIf != null && !q.OnlyIf(scraped))
                    {
                        LogInfo("[" + biomarkerName + "] " + q.Label + " (" + q.CKey +
                                "): skipped — OnlyIf condition not met.");
                        continue;
                    }
        
                    if (q.Type == "multiPickPerItem")
                    {
                        ResolveMultiPickPerItem(q, scraped, biomarkerName);
                        continue;
                    }
        
                    string answer = ResolveAnswer(q, scraped);
        
                    if (answer != null)
                    {
                        _speechBox.ActiveDocument.FindField(q.CKey).Value = answer;
                        _speechBox.RefreshDocumentChanges();
                        LogInfo("[" + biomarkerName + "] " + q.Label + ": " + q.CKey + " <- " + answer);
                    }
                    else
                    {
                        LogWarning("[" + biomarkerName + "] " + q.Label + " (" + q.CKey +
                                   "): could not resolve answer. AiProperty=" + q.AiProperty +
                                   " rawValue='" + (scraped.Get(q.AiProperty) ?? "<null>") + "'");
                    }
                }
            }
        
            FlushMultiPicks();
        
            // The adapter's scraping (GetAndFocusBrowser) leaves the vendor's IMS
            // window in the foreground -- hand focus back to Report Builder so
            // the pathologist doesn't have to switch windows manually. Same
            // pattern _ChecklistAutoInserter uses after inserting a checklist.
            WindowTools.Instance.EnsureForegroundWindow("Report Builder");
        }

        // ----------------------------------------------------------------
        //   Scraping
        // ----------------------------------------------------------------

        ScrapedValues ScrapeBiomarkerValues(string biomarkerName, BiomarkerMap map)
        {
            var result = new ScrapedValues { Biomarker = biomarkerName };

            foreach (var q in map.Questions)
            {
                if (q.Type == "multiPickPerItem")
                {
                    if (q.MultiPickItems == null) continue;

                    var perItem = new Dictionary<string, string>();
                    foreach (var item in q.MultiPickItems)
                    {
                        string itemLabel = LookupLabel(biomarkerName + "." + item.ItemKey);
                        if (string.IsNullOrEmpty(itemLabel))
                        {
                            // Vendor doesn't support this item — silently skip.
                            continue;
                        }
                        string v = _source.ReadValueByLabel(itemLabel);
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            perItem[item.ItemKey] = v.Trim();
                            LogInfo("[" + biomarkerName + "] scraped item " +
                                    item.ItemKey + " ('" + itemLabel + "') = '" + v.Trim() + "'");
                        }
                    }
                    if (perItem.Count > 0)
                        result.ExtraFields[biomarkerName] = perItem;
                    continue;
                }

                // Skip questions with no logical AiProperty (e.g. computed-only)
                if (string.IsNullOrEmpty(q.AiProperty))
                    continue;

                // De-dup: don't re-scrape the same property twice in one biomarker
                if (result.Values.ContainsKey(q.AiProperty)) continue;

                string label = LookupLabel(biomarkerName + "." + q.AiProperty);
                if (string.IsNullOrEmpty(label))
                {
                    // Vendor's overlay doesn't include this property — silently skip.
                    // The biomarker as a whole will likely "no values scraped" and
                    // log that, which is enough signal for the operator.
                    continue;
                }

                string raw = _source.ReadValueByLabel(label);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    result.Values[q.AiProperty] = raw.Trim();
                    LogInfo("[" + biomarkerName + "] scraped " + q.AiProperty +
                            " ('" + label + "') = '" + raw.Trim() + "'");
                }
            }

            return result;
        }

        string LookupLabel(string overlayKey)
        {
            string label;
            return _labels.TryGetValue(overlayKey, out label) ? label : null;
        }

        // ----------------------------------------------------------------
        //   Resolvers
        // ----------------------------------------------------------------

        string ResolveAnswer(QuestionMap q, ScrapedValues scraped)
        {
            switch (q.Type)
            {
                case "categorical":
                {
                    string val = scraped.Get(q.AiProperty);
                    if (string.IsNullOrEmpty(val)) return null;
                    string cKey;
                    return q.ValueMap != null && q.ValueMap.TryGetValue(val, out cKey) ? cKey : null;
                }

                case "percentageRange":
                {
                    string val = scraped.Get(q.AiProperty);
                    if (string.IsNullOrEmpty(val)) return null;
                    val = val.Replace("%", "").Trim();
                    double num;
                    if (!double.TryParse(val, System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out num))
                        return null;
                    foreach (var rule in q.RangeMap)
                        if (num >= rule.Min && num <= rule.Max) return rule.Answer;
                    return null;
                }

                case "computed":
                    return q.Compute == null ? null : q.Compute(scraped);

                case "text":
                {
                    // Write the scraped string directly to the CAP field, verbatim.
                    return scraped.Get(q.AiProperty);
                }
                
                case "numericText":
                {
                    // Free-form numeric field. Strip %, validate as a number,
                    // leave blank if invalid.
                    string val = scraped.Get(q.AiProperty);
                    if (string.IsNullOrEmpty(val)) return null;
                    val = val.Replace("%", "").Trim();
                    double num;
                    if (!double.TryParse(val, System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out num))
                        return null;
                    return num.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                default:
                    return null;
            }
        }

        void ResolveMultiPickPerItem(QuestionMap q, ScrapedValues scraped, string biomarkerName)
        {
            if (q.MultiPickItems == null || q.MultiPickItems.Count == 0)
            {
                LogWarning("[" + biomarkerName + "] " + q.Label + " (" + q.CKey +
                           "): multiPickPerItem with no MultiPickItems configured.");
                return;
            }

            Dictionary<string, string> perItem = null;
            if (scraped.ExtraFields != null && scraped.ExtraFields.ContainsKey(biomarkerName))
                perItem = scraped.ExtraFields[biomarkerName];

            if (perItem == null || perItem.Count == 0)
            {
                LogInfo("[" + biomarkerName + "] " + q.Label +
                        ": no per-item data scraped; skipping.");
                return;
            }

            foreach (var item in q.MultiPickItems)
            {
                if (!perItem.ContainsKey(item.ItemKey))
                    continue;

                // (a) queue the parent MultiPick selection — flushed in one
                // pipe-delimited call at end of Run().
                QueueMultiPickAnswer(q.CKey, item.ItemCKey);
                LogInfo("[" + biomarkerName + "] " + q.Label + " queued <- " + item.ItemCKey +
                        " (item: " + item.ItemKey + ")");

                // (b) sub-question — typically PickList (single-value), written directly.
                if (!string.IsNullOrEmpty(item.SubQuestionCKey) && item.SubValueMap != null)
                {
                    string rawValue = perItem[item.ItemKey];
                    string subAnswer;
                    if (item.SubValueMap.TryGetValue(rawValue, out subAnswer))
                    {
                        _speechBox.ActiveDocument.FindField(item.SubQuestionCKey).Value = subAnswer;
                        _speechBox.RefreshDocumentChanges();
                        LogInfo("  [" + item.ItemKey + "] " + item.SubQuestionCKey + " <- " + subAnswer);
                    }
                    else
                    {
                        LogWarning("[" + biomarkerName + "] item '" + item.ItemKey +
                                   "' value '" + rawValue + "' not in SubValueMap; sub-question left blank.");
                    }
                }
            }
        }

        // ----------------------------------------------------------------
        //   MultiPick queue/flush
        // ----------------------------------------------------------------

        void QueueMultiPickAnswer(string questionCKey, string answerCKey)
        {
            if (string.IsNullOrEmpty(questionCKey) || string.IsNullOrEmpty(answerCKey)) return;
            HashSet<string> set;
            if (!_pendingMultiPicks.TryGetValue(questionCKey, out set))
            {
                set = new HashSet<string>();
                _pendingMultiPicks[questionCKey] = set;
            }
            set.Add(answerCKey);
        }

        void FlushMultiPicks()
        {
            foreach (var kvp in _pendingMultiPicks)
            {
                string qCKey = kvp.Key;
                var answers = kvp.Value;
                if (answers.Count == 0) continue;

                string joined = string.Join("|", answers);
                try
                {
                    var field = _speechBox.ActiveDocument.FindField(qCKey);
                    if (field == null)
                    {
                        LogWarning("[multipick-flush] " + qCKey +
                                   " not found in template; skipping " + answers.Count + " queued selection(s).");
                        continue;
                    }
                    field.Value = joined;
                    _speechBox.RefreshDocumentChanges();
                    LogInfo("[multipick-flush] " + qCKey + " <- " + joined +
                            " (" + answers.Count + " answer" + (answers.Count == 1 ? "" : "s") + ")");
                }
                catch (Exception ex)
                {
                    LogWarning("[multipick-flush] " + qCKey + " write threw: " + ex.Message);
                }
            }
        }

        // ----------------------------------------------------------------
        //   Logging
        // ----------------------------------------------------------------

        void LogInfo(string message)    { StatusLog.WriteInformationEntry(message); }
        void LogWarning(string message) { StatusLog.WriteWarningEntry(message); }
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE
