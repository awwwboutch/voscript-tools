// PREREQUISITE: VOScript.Starter.CapResultParser.Mappings._BreastBmk169Mapping
// Source revision 3P-3501-16, exported 2026-09-28 from demo\sales.
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

namespace VOScript.Starter.CapResultParser.Mappings
{
    #region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    #endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

    /// <summary>
    /// CAP Breast Biomarker Reporting (Bmk169) — Jun 2025 release.
    /// XML source: za25CAP-Jun25-BreastBmk169_2P-4831-1.xml
    /// Generated:  via cap_converter.html, verified 2026-05-12
    ///
    /// CAP-side only — contains CKeys, ValueMaps, RangeMaps, gates, OnlyIf.
    /// AI-side labels (what shows on a vendor's UI) live in per-vendor overlay
    /// files like _HarnessBreastBmk169Labels.cs.
    ///
    /// AUDIT TRAIL — CKey provenance per biomarker:
    ///   ✅ ER       — converter-generated, verified against XML
    ///   ✅ PR       — converter-generated, verified against XML
    ///   ✅ HER2     — converter-generated, verified against XML
    /// </summary>
    // CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class _BreastBmk169Mapping : ExtensionScript
    {
        public static Dictionary<string, BiomarkerMap> Build()
        {
            return new Dictionary<string, BiomarkerMap>
            {
                // ============================================================
                // ESTROGEN RECEPTOR (ER)
                // SOURCE: ✅ VERIFIED — za25CAP-Jun25-BreastBmk169_2P-4831-1.xml
                // OVERLAY KEYS: "ER.Status", "ER.PositivityPercent", "ER.Intensity"
                // ============================================================
                ["ER"] = new BiomarkerMap
                {
                    GateQuestionCKey = "C758212_100004300",
                    GateAnswerCKey   = "C31160_100004300",
                    Questions = new List<QuestionMap>
                    {
                        new QuestionMap
                        {
                            CKey          = "C49025_100004300",
                            Label         = "Estrogen Receptor (ER) Status",
                            Type          = "computed",
                            AiProperty    = "Status",
                            ValueMap = new Dictionary<string, string>
                            {
                                ["Positive"]             = "C48890_100004300",
                                ["Low"]                  = "C48919_100004300",
                                ["Low positive"]         = "C48919_100004300",
                                ["Negative"]             = "C29915_100004300",
                                ["Cannot be determined"] = "C977014_100004300"
                            },
                            Compute = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                {
                                    switch (status)
                                    {
                                        case "Positive": return "C48890_100004300";
                                        case "Low":
                                        case "Low positive": return "C48919_100004300";
                                        case "Negative": return "C29915_100004300";
                                        case "Cannot be determined": return "C977014_100004300";
                                        default: return null;
                                    }
                                }
                                double pct;
                                if (!double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct))
                                    return null;
                                // ASCO/CAP focused update (Allison et al., J Clin Oncol 2020; PMID
                                // 31928404): <1% = Negative; 1-10% = ER Low Positive (report with
                                // recommended comment re: limited data on endocrine therapy
                                // benefit in that range); >10% = Positive.
                                if (pct < 1) return "C29915_100004300";
                                if (pct <= 10) return "C48919_100004300";
                                return "C48890_100004300";
                            }
                        },
                        new QuestionMap
                        {
                            CKey          = "C48899_100004300",
                            Label         = "ER: Percentage of Cells with Nuclear Staining",
                            Type          = "percentageRange",
                            AiProperty    = "PositivityPercent",
                            OnlyIf        = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                    return status == "Positive" || status == "Low" || status == "Low positive";
                                // No explicit Status text scraped (e.g. Halo) — fall back to
                                // the raw percentage against the ASCO/CAP >=1% positivity floor.
                                double pct;
                                return double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct) && pct >= 1;
                            },
                            RangeMap = new List<RangeRule>
                            {
                                new RangeRule { Min = 1,  Max = 10,  Answer = "C29748_100004300" },
                                new RangeRule { Min = 11, Max = 20,  Answer = "C32075_100004300" },
                                new RangeRule { Min = 21, Max = 30,  Answer = "C32076_100004300" },
                                new RangeRule { Min = 31, Max = 40,  Answer = "C32079_100004300" },
                                new RangeRule { Min = 41, Max = 50,  Answer = "C32080_100004300" },
                                new RangeRule { Min = 51, Max = 60,  Answer = "C32081_100004300" },
                                new RangeRule { Min = 61, Max = 70,  Answer = "C32082_100004300" },
                                new RangeRule { Min = 71, Max = 80,  Answer = "C32083_100004300" },
                                new RangeRule { Min = 81, Max = 90,  Answer = "C32077_100004300" },
                                new RangeRule { Min = 91, Max = 100, Answer = "C32078_100004300" }
                            }
                        },
                        new QuestionMap
                        {
                            CKey          = "C29749_100004300",
                            Label         = "ER: Average Intensity of Staining",
                            Type          = "categorical",
                            AiProperty    = "Intensity",
                            OnlyIf        = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                    return status == "Positive" || status == "Low" || status == "Low positive";
                                double pct;
                                return double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct) && pct >= 1;
                            },
                            ValueMap = new Dictionary<string, string>
                            {
                                // Text form (Harness/Corista scrape a categorical label directly)
                                ["Weak"]     = "C29811_100004300",
                                ["Moderate"] = "C29812_100004300",
                                ["Strong"]   = "C29914_100004300",
                                // Numeric form (Halo emits a raw 1-3 intensity score, no text label)
                                ["1"]        = "C29811_100004300",
                                ["2"]        = "C29812_100004300",
                                ["3"]        = "C29914_100004300",
                                // Descriptive form (VBPathView emits "Weak (1+)" etc., not bare text/number)
                                ["Weak (1+)"]     = "C29811_100004300",
                                ["Moderate (2+)"] = "C29812_100004300",
                                ["Strong (3+)"]   = "C29914_100004300"
                            }
                        }
                    }
                },

                // ============================================================
                // PROGESTERONE RECEPTOR (PR)
                // SOURCE: ✅ VERIFIED — za25CAP-Jun25-BreastBmk169_2P-4831-1.xml
                // OVERLAY KEYS: "PR.Status", "PR.PositivityPercent", "PR.Intensity"
                // ============================================================
                ["PR"] = new BiomarkerMap
                {
                    GateQuestionCKey = "C758212_100004300",
                    GateAnswerCKey   = "C31161_100004300",
                    Questions = new List<QuestionMap>
                    {
                        new QuestionMap
                        {
                            CKey          = "C30524_100004300",
                            Label         = "Progesterone Receptor (PgR) Status",
                            Type          = "computed",
                            AiProperty    = "Status",
                            ValueMap = new Dictionary<string, string>
                            {
                                ["Positive"]             = "C30533_100004300",
                                ["Negative"]             = "C31001_100004300",
                                ["Cannot be determined"] = "C977022_100004300"
                            },
                            Compute = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                {
                                    switch (status)
                                    {
                                        case "Positive": return "C30533_100004300";
                                        case "Negative": return "C31001_100004300";
                                        case "Cannot be determined": return "C977022_100004300";
                                        default: return null;
                                    }
                                }
                                double pct;
                                if (!double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct))
                                    return null;
                                // Same ASCO/CAP >=1% positivity floor applied to PR — this CAP
                                // template has no separate "PR Low Positive" category.
                                return pct < 1 ? "C31001_100004300" : "C30533_100004300";
                            }
                        },
                        new QuestionMap
                        {
                            CKey          = "C32094_100004300",
                            Label         = "PR: Percentage of Cells with Nuclear Staining",
                            Type          = "percentageRange",
                            AiProperty    = "PositivityPercent",
                            OnlyIf        = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                    return status == "Positive";
                                double pct;
                                return double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct) && pct >= 1;
                            },
                            RangeMap = new List<RangeRule>
                            {
                                new RangeRule { Min = 1,  Max = 10,  Answer = "C32097_100004300" },
                                new RangeRule { Min = 11, Max = 20,  Answer = "C32098_100004300" },
                                new RangeRule { Min = 21, Max = 30,  Answer = "C32099_100004300" },
                                new RangeRule { Min = 31, Max = 40,  Answer = "C32100_100004300" },
                                new RangeRule { Min = 41, Max = 50,  Answer = "C32101_100004300" },
                                new RangeRule { Min = 51, Max = 60,  Answer = "C32102_100004300" },
                                new RangeRule { Min = 61, Max = 70,  Answer = "C32103_100004300" },
                                new RangeRule { Min = 71, Max = 80,  Answer = "C32104_100004300" },
                                new RangeRule { Min = 81, Max = 90,  Answer = "C32105_100004300" },
                                new RangeRule { Min = 91, Max = 100, Answer = "C32106_100004300" }
                            }
                        },
                        new QuestionMap
                        {
                            CKey          = "C30564_100004300",
                            Label         = "PR: Average Intensity of Staining",
                            Type          = "categorical",
                            AiProperty    = "Intensity",
                            OnlyIf        = vals =>
                            {
                                string status = vals.Get("Status");
                                if (!string.IsNullOrEmpty(status))
                                    return status == "Positive";
                                double pct;
                                return double.TryParse((vals.Get("PositivityPercent") ?? "").Replace("%", "").Trim(),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out pct) && pct >= 1;
                            },
                            ValueMap = new Dictionary<string, string>
                            {
                                ["Weak"]     = "C30565_100004300",
                                ["Moderate"] = "C30999_100004300",
                                ["Strong"]   = "C31000_100004300",
                                ["1"]        = "C30565_100004300",
                                ["2"]        = "C30999_100004300",
                                ["3"]        = "C31000_100004300",
                                ["Weak (1+)"]     = "C30565_100004300",
                                ["Moderate (2+)"] = "C30999_100004300",
                                ["Strong (3+)"]   = "C31000_100004300"
                            }
                        }
                    }
                },

                // ============================================================
                // HER2 by IHC
                // SOURCE: ✅ VERIFIED — za25CAP-Jun25-BreastBmk169_2P-4831-1.xml
                // OVERLAY KEY: "HER2.Score"
                // ============================================================
                ["HER2"] = new BiomarkerMap
                {
                    GateQuestionCKey = "C758212_100004300",
                    GateAnswerCKey   = "C31163_100004300",
                    Questions = new List<QuestionMap>
                    {
                        new QuestionMap
                        {
                            CKey          = "C977030_100004300",
                            Label         = "HER2 by Immunohistochemistry (IHC) Status",
                            Type          = "categorical",
                            AiProperty    = "Score",
                            ValueMap = new Dictionary<string, string>
                            {
                                ["0"]                    = "C977032_100004300",
                                ["Score 0"]              = "C977032_100004300",
                                ["1"]                    = "C977037_100004300",
                                ["Score 1"]              = "C977037_100004300",
                                ["2"]                    = "C977042_100004300",
                                ["Score 2"]              = "C977042_100004300",
                                ["3"]                    = "C977051_100004300",
                                ["Score 3"]              = "C977051_100004300",
                                // Descriptive form (VBPathView emits bare "1+"/"2+"/"3+", matching
                                // CAP's own "+" convention for nonzero scores)
                                ["1+"]                   = "C977037_100004300",
                                ["2+"]                   = "C977042_100004300",
                                ["3+"]                   = "C977051_100004300",
                                ["Other"]                = "C977066_100004300",
                                ["Cannot be determined"] = "C977067_100004300"
                            }
                        }
                    }
                },
                
                // ============================================================
                // Ki-67
                // SOURCE: ✅ VERIFIED — za25CAP-Jun25-BreastBmk169_2P-4831-1.xml
                // OVERLAY KEY: "Ki-67.ProliferationIndex"
                // ============================================================
                ["Ki-67"] = new BiomarkerMap
                {
                    GateQuestionCKey = "C758212_100004300",
                    GateAnswerCKey   = "C31165_100004300",
                    Questions = new List<QuestionMap>
                    {
                        new QuestionMap
                        {
                            CKey          = "C977108_100004300",
                            Label         = "Ki-67 Proliferative Index",
                            Type          = "percentageRange",
                            AiProperty    = "ProliferationIndex",
                            RangeMap = new List<RangeRule>
                            {
                                new RangeRule { Min = 0,  Max = 5,   Answer = "C977112_100004300" },
                                new RangeRule { Min = 6,  Max = 10,  Answer = "C977113_100004300" },
                                new RangeRule { Min = 11, Max = 15,  Answer = "C977114_100004300" },
                                new RangeRule { Min = 16, Max = 20,  Answer = "C977115_100004300" },
                                new RangeRule { Min = 21, Max = 30,  Answer = "C977116_100004300" },
                                new RangeRule { Min = 31, Max = 40,  Answer = "C977117_100004300" },
                                new RangeRule { Min = 41, Max = 50,  Answer = "C977118_100004300" },
                                new RangeRule { Min = 51, Max = 60,  Answer = "C977119_100004300" },
                                new RangeRule { Min = 61, Max = 70,  Answer = "C977120_100004300" },
                                new RangeRule { Min = 71, Max = 80,  Answer = "C977121_100004300" },
                                new RangeRule { Min = 81, Max = 90,  Answer = "C977122_100004300" },
                                new RangeRule { Min = 91, Max = 100, Answer = "C977123_100004300" }
                            }
                        }
                    }
                }
            };
        }
    }

#region CLOSEOUT BOILERPLATE
} // close namespace
#endregion CLOSEOUT BOILERPLATE