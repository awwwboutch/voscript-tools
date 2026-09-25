#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
using VOScript.Standard.SpeechBox;  // For ListTools
#endregion USING NAMESPACES

namespace VOScript.Starter.Lumea
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
    [ExtensionCommandClass(HelpText = "Takes a picture with the Lumea grossing camera and fills the gross from it. Prostate: builds the six cores on the BxChip (say it twice for a 12-core case). Any other specimen: fills its size. Standard trigger: take photo")]
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    public class LumeaCameraCapture : CommandScript
    {
        // Author: andrew.boutcher@voicebrook.com
        // Date: 9/25/2026
        // Use:
        // One command for every specimen. What is already in the gross decides what the camera does:
        //
        //  - PROSTATE (BxChip). Runs when the gross is still the untouched placeholder, or the
        //    grosser is on a prostate specimen. A 12-core case is two chips, one per side:
        //      first photo  - gross untouched  -> clears the placeholder, builds parts A-F
        //      second photo - six cores there  -> appends parts G-L
        //      third photo  - twelve there     -> refuses; the prostate is complete
        //    Anything else is left alone. Nothing already dictated is ever overwritten.
        //
        //  - ANYTHING ELSE (generic). The camera cannot tell what it is looking at, so the grosser
        //    inserts the template first; the photo then fills Size (length x width - the camera
        //    has no depth) and the fragment count on the specimen they are on.
        //
        // Per-site settings (template names, lane-to-site order, simulate mode) are all in
        // _LumeaCamera's SITE CONFIGURATION region.
        //
        // *****

        private const string GrossListName = "GrossList";
        private const string SizeUnit = " cm";

        public override void Execute()
        {
            try
            {
                if (SpeechBox.ActiveDocument == null)
                {
                    ShowWarning("No Report", "Open the case in Report Builder before taking a photo.");
                    return;
                }

                IDocList grossList = SpeechBox.ActiveDocument.FindActivePart(GrossListName, DocPartTypes.List) as IDocList;
                if (grossList == null)
                {
                    ShowWarning("No Gross List", $"This document has no \"{GrossListName}\" list to put the photo's measurements into.");
                    return;
                }

                IDocListItem current = FindCurrentSpecimen(grossList);
                bool placeholderOnly = IsPlaceholderOnly(grossList);
                bool onProstate = current != null && IsTemplate(current, _LumeaCamera.ProstateTemplate);

                StatusLog.WriteInformationEntry($"LumeaCameraCapture: case {SpeechBox.ActiveDocument.CaseNumber}, {grossList.Items.Count} gross item(s), placeholder only = {placeholderOnly}, on prostate = {onProstate}, current PartKey = \"{current?.PartKey ?? "(none)"}\", prostate library PartKey = \"{LibraryKey(_LumeaCamera.ProstateTemplate) ?? "(not found)"}\".");

                if (placeholderOnly || onProstate)
                    CaptureProstate(grossList, placeholderOnly);
                else
                    CaptureSpecimen(current);
            }
            catch (Exception ex)
            {
                StatusLog.WriteErrorEntry("LumeaCameraCapture failed: " + ex.Message, ex);
                ShowWarning("Photo Failed", ex.Message);
            }
        } // close Execute

        // ------------------------------------------------------------------
        // Prostate: one BxChip = six cores
        // ------------------------------------------------------------------

        private void CaptureProstate(IDocList grossList, bool placeholderOnly)
        {
            int prostateCount = grossList.Items.Cast<IDocListItem>().Count(i => IsTemplate(i, _LumeaCamera.ProstateTemplate));
            string[] sites;

            // Decide which half this is BEFORE taking the photo, so a photo that would be refused
            // is never taken.
            if (placeholderOnly)
                sites = _LumeaCamera.FirstHalfSites;
            else if (prostateCount == _LumeaCamera.LaneCount)
                sites = _LumeaCamera.SecondHalfSites;
            else if (prostateCount >= _LumeaCamera.LaneCount * 2)
            {
                ShowWarning("Prostate Complete", $"This case already has {prostateCount} prostate cores. Nothing was added.");
                return;
            }
            else
            {
                ShowWarning("Not Changed", $"The gross already has {prostateCount} prostate specimen(s). A chip photo needs an untouched gross (first chip) or exactly {_LumeaCamera.LaneCount} cores (second chip), so nothing was changed.");
                return;
            }

            _LumeaCamera.CaptureResult result = _LumeaCamera.CaptureChip();
            if (!CheckResult(result)) return;

            if (result.Lanes.Count != _LumeaCamera.LaneCount)
            {
                ShowWarning("Unexpected Result", $"The camera returned {result.Lanes.Count} lanes instead of {_LumeaCamera.LaneCount}. Nothing was changed.");
                return;
            }

            if (result.Lanes.All(lane => lane.Count == 0))
            {
                ShowWarning("No Tissue Found", "The camera found no tissue in any lane. Check the BxChip is under the camera and try again. Nothing was changed.");
                return;
            }

            // Only now that there is something to put in: the placeholder goes.
            if (placeholderOnly)
            {
                grossList.Items.Clear();
                SpeechBox.RefreshDocumentChanges();
            }

            int firstNew = grossList.Items.Count;
            List<string> emptyParts = new List<string>();
            IDocListItem firstEmptyPart = null;

            for (int lane = 0; lane < _LumeaCamera.LaneCount; lane++)
            {
                IDocListItem part = SpeechBox.FindLibraryPart(GrossListName + "." + _LumeaCamera.ProstateTemplate) as IDocListItem;
                if (part == null)
                    throw new ClientException($"The {_LumeaCamera.ProstateTemplate} template could not be found in the {GrossListName} library. {lane} of {_LumeaCamera.LaneCount} cores were added.");

                grossList.Items.Add(part);
                SpeechBox.RefreshDocumentChanges();

                string letter = PartLetter(grossList.Items.Count - 1);
                List<_LumeaCamera.Piece> pieces = result.Lanes[lane];

                SetField(part, new[] { _LumeaCamera.SiteField }, sites[lane], letter);

                // Every lane still gets its part, empty or not: parts are matched to the LIS by
                // position, so skipping one would shift every core after it onto the wrong part.
                if (pieces.Count == 0)
                {
                    emptyParts.Add(letter);
                    if (firstEmptyPart == null) firstEmptyPart = part;
                    StatusLog.WriteWarningEntry($"LumeaCameraCapture: lane {lane + 1} ({sites[lane]}, part {letter}) had no tissue; size left blank.");
                    continue;
                }

                SetField(part, _LumeaCamera.SizeFields, _LumeaCamera.ToCm(pieces.Sum(p => p.Length)) + SizeUnit, letter);
                SetCount(part, pieces.Count, letter);
            }

            SpeechBox.RefreshDocumentChanges();

            StatusLog.WriteInformationEntry($"LumeaCameraCapture: added parts {PartLetter(firstNew)}-{PartLetter(grossList.Items.Count - 1)} ({(sites == _LumeaCamera.FirstHalfSites ? "first" : "second")} chip).");

            if (firstEmptyPart == null)
            {
                SpeechBox.SetInputFocus(grossList.Items[firstNew]);
                return;
            }

            ShowWarning("Empty Lanes", $"No tissue was found for part(s) {string.Join(", ", emptyParts)}. Their sizes are blank - dictate them, or re-seat the chip and check the photo.");

            // After the dialog, so it does not take the focus back: the cursor waits in the first
            // blank size, ready for the grosser to dictate it.
            string sizeField = _LumeaCamera.SizeFields.FirstOrDefault(name => firstEmptyPart.FindField(name) != null);
            if (sizeField != null)
                SpeechBox.SetInputFocus(firstEmptyPart.FindField(sizeField));
            else
                SpeechBox.SetInputFocus(firstEmptyPart);
        }

        // ------------------------------------------------------------------
        // Any other specimen: fill the size on the current one
        // ------------------------------------------------------------------

        private void CaptureSpecimen(IDocListItem current)
        {
            if (current == null)
            {
                ShowWarning("No Specimen", "Insert the specimen's template first, then take the photo.");
                return;
            }

            string letter = PartLetter(current.Index);

            // Checked before the photo: with nowhere to put the size, the photo is pointless.
            string existingSizeField = _LumeaCamera.SizeFields.FirstOrDefault(name => current.FindField(name) != null);
            if (existingSizeField == null)
            {
                ShowWarning("No Size Field", $"Part {letter}'s template ({TemplateOf(current)}) has no size field, so there is nowhere to put the measurement. Insert the specimen's template first, then take the photo.");
                return;
            }

            // Never overwrite: a size already there was dictated or captured on purpose.
            string existingSize = current.FindField(existingSizeField).Value;
            if (!string.IsNullOrWhiteSpace(existingSize))
            {
                ShowWarning("Size Already Filled", $"Part {letter} already has a size ({existingSize}). Nothing was changed. Move to the next specimen, or clear the size first to replace it.");
                return;
            }

            _LumeaCamera.CaptureResult result = _LumeaCamera.CaptureGeneric();
            if (!CheckResult(result)) return;

            if (result.Pieces.Count == 0)
            {
                ShowWarning("No Tissue Found", "The camera found no tissue. Check the specimen is in view and try again.");
                return;
            }

            // One piece: its own length x width. Several: the largest length x the largest width,
            // with the count going into the fragment field.
            double length = result.Pieces.Max(p => p.Length);
            double width = result.Pieces.Max(p => p.Width);
            if (result.Pieces.Count > 1)
                StatusLog.WriteInformationEntry($"LumeaCameraCapture: {result.Pieces.Count} pieces on part {letter}; size uses the largest length and width.");

            string sizeField = SetField(current, _LumeaCamera.SizeFields, $"{_LumeaCamera.ToCm(length)} x {_LumeaCamera.ToCm(width)}{SizeUnit}", letter);
            SetCount(current, result.Pieces.Count, letter);

            SpeechBox.RefreshDocumentChanges();

            // Leave the cursor on the measurement just filled.
            if (sizeField != null)
                SpeechBox.SetInputFocus(current.FindField(sizeField));
            else
                SpeechBox.SetInputFocus(current);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private bool CheckResult(_LumeaCamera.CaptureResult result)
        {
            if (result.Success) return true;

            StatusLog.WriteWarningEntry($"LumeaCameraCapture: capture failed - {result.ErrorId}: {result.ErrorMessage}");
            ShowWarning("Photo Failed", _LumeaCamera.DescribeError(result) + " Nothing was changed.");
            return false;
        }

        /// <summary>
        /// True while the gross is still what a new document starts with: nothing, or only the
        /// placeholder specimen with nothing dictated into it.
        /// </summary>
        /// <remarks>
        /// An untouched placeholder is not blank: it renders its part label ("A."), which Report
        /// Builder adds by itself (prompt text such as "Cassettes:" does not render). So the label
        /// is stripped before looking for dictation. If anything is left, it is compared with a
        /// fresh copy of the placeholder, in case a site's placeholder has fixed text of its own.
        /// </remarks>
        private bool IsPlaceholderOnly(IDocList grossList)
        {
            string freshText = null;

            foreach (IDocListItem item in grossList.Items)
            {
                if (item == null) continue;
                if (!IsTemplate(item, _LumeaCamera.PlaceholderTemplate)) return false;

                string itemText = Normalise(item.RenderText(RenderFlags.None));
                if (!itemText.Any(char.IsLetterOrDigit)) continue;

                if (freshText == null)
                {
                    IDocListItem fresh = SpeechBox.FindLibraryPart(GrossListName + "." + _LumeaCamera.PlaceholderTemplate) as IDocListItem;
                    freshText = fresh == null ? "" : Normalise(fresh.RenderText(RenderFlags.None));
                }

                if (itemText == freshText) continue;

                StatusLog.WriteInformationEntry($"LumeaCameraCapture: placeholder has been dictated into, so the gross is not fresh. Item: \"{Truncate(itemText)}\" / fresh template: \"{Truncate(freshText)}\".");
                return false;
            }
            return true;
        }

        // Whitespace removed, so a stray space or line break does not count as dictation, and the
        // leading part label ("A.", "1)", "(B)") dropped, since Report Builder adds it by itself.
        private static string Normalise(string text)
        {
            string squashed = new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
            return Regex.Replace(squashed, @"^\(?[A-Za-z0-9]{1,2}[.)]", "");
        }

        private static string Truncate(string s) => s.Length <= 120 ? s : s.Substring(0, 120) + "...";

        // The GrossList item the cursor is in. Falls back to the last specimen when focus is
        // somewhere else (e.g. the grosser clicked away after inserting the template).
        private IDocListItem FindCurrentSpecimen(IDocList grossList)
        {
            if (grossList.Items.Count == 0) return null;

            IDocListItem focused = SpeechBox.InputFocus == null
                ? null
                : ListTools.FindTopListItem(SpeechBox.InputFocus) as IDocListItem;

            for (int i = 0; i < grossList.Items.Count; i++)
            {
                if (focused != null && ReferenceEquals(grossList.Items[i], focused))
                    return focused;
            }

            return grossList.Items[grossList.Items.Count - 1] as IDocListItem;
        }

        // "GrossList.ProstateBiopsy" -> "ProstateBiopsy". For display only; matching goes through
        // IsTemplate.
        private static string TemplateOf(IDocListItem item)
        {
            string key = item?.PartKey ?? "";
            return key.Substring(key.LastIndexOf('.') + 1);
        }

        private readonly Dictionary<string, string> libraryKeys = new Dictionary<string, string>();

        // The PartKey a fresh copy of this template carries, looked up once per run.
        private string LibraryKey(string template)
        {
            if (!libraryKeys.TryGetValue(template, out string key))
            {
                key = (SpeechBox.FindLibraryPart(GrossListName + "." + template) as IDocListItem)?.PartKey;
                libraryKeys[template] = key;
            }
            return key;
        }

        // Whether a specimen was made from this template. Only the template name - the last
        // segment of the key - is compared, because what comes before it differs between a
        // specimen in the document and the library's copy. Report Builder also numbers the key of
        // every further copy (ProstateBiopsy, ProstateBiopsy2, ...), so that number is stripped
        // from both sides - which keeps a template whose own name ends in a digit (Biopsy1)
        // matching too.
        private static bool IsTemplate(IDocListItem item, string template)
        {
            if (item == null || string.IsNullOrEmpty(item.PartKey)) return false;
            return string.Equals(StripCopyNumber(TemplateOf(item)), StripCopyNumber(template), StringComparison.OrdinalIgnoreCase);
        }

        // "ProstateBiopsy6" / "ProstateBiopsy_6" -> "ProstateBiopsy"
        private static string StripCopyNumber(string name) => Regex.Replace(name ?? "", @"[\W_]*\d+$", "");

        private static string PartLetter(int index) => index >= 0 && index < 26 ? ((char)('A' + index)).ToString() : (index + 1).ToString();

        // Returns the name of the field that was filled, or null when the template has none of them.
        private string SetField(IDocListItem part, string[] fieldNames, string value, string letter)
        {
            foreach (string name in fieldNames)
            {
                var field = part.FindField(name);
                if (field == null) continue;
                field.Value = value;
                return name;
            }
            StatusLog.WriteWarningEntry($"LumeaCameraCapture: part {letter} has no field named {string.Join(" / ", fieldNames)}, so \"{value}\" was not placed.");
            return null;
        }

        private void SetCount(IDocListItem part, int count, string letter)
        {
            if (string.IsNullOrWhiteSpace(_LumeaCamera.FragmentCountField)) return;
            if (part.FindField(_LumeaCamera.FragmentCountField) == null) return;   // Optional: not every template counts pieces.
            SetField(part, new[] { _LumeaCamera.FragmentCountField }, _LumeaCamera.CountAsWord(count), letter);
        }

        private void ShowWarning(string title, string message)
        {
            SpeechDialogSettings dialogSettings = new SpeechDialogSettings(SpeechDialogSettings.IconTypes.Warning, title, message);
            dialogSettings.AddChoice("OK", "");
            Application.ShowSpeechDialog(dialogSettings);
            StatusLog.WriteInformationEntry(title + ": " + message);
        }

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
