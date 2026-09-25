#region USING NAMESPACES
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VoiceOver.Common;
using VoiceOver.Extensions;
using VoiceOver.InternalScripts;
#endregion USING NAMESPACES

namespace VOScript.Starter.Lumea
{
#region PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION
#endregion PROPERTYDEF: COMMAND PALETTE PROPERTIES - DO NOT CHANGE CODE IN THIS REGION

// CLASSDEF: KEEP THE NEXT LINE IN 'public class ScriptName : BaseClassName' FORMAT
    /// <summary>
    /// Talks to the Lumea grossing camera (Lumea Grossing Camera API v2.0.1) and turns what it
    /// returns into plain measurements.
    ///
    /// What the camera does and does not know - read this before changing the command:
    ///  1. The camera is local. It serves its API on the machine it is attached to, at the same
    ///     address on every site. Nothing goes to Lumea's cloud to take or measure a picture.
    ///  2. It returns measurements only: length, area and (generic action) width, in mm / mm2.
    ///     Nothing in the response identifies the case, the specimen or the site. On a BxChip
    ///     the lane position is the only identity, which is why prostate can be automated and
    ///     nothing else can.
    ///  3. There is no depth. A generic capture gives length x width at most.
    ///  4. Which algorithm runs is our choice, sent in the request - the camera does not work
    ///     out that it is looking at a prostate.
    /// </summary>
    public class _LumeaCamera : ExtensionScript
    {
        #region SITE CONFIGURATION -- THE ONLY REGION THAT SHOULD NEED EDITING PER CUSTOMER

        // Constants rather than command palette properties: palette properties have not been
        // reliably reaching scripts, and these are per-site decisions, not per-user ones.

        /// <summary>
        /// TRUE feeds canned responses through the full code path instead of calling the camera.
        /// Use it to test without a camera. SET TO FALSE ON ANY MACHINE WITH A REAL CAMERA.
        /// Every simulated capture says so in the log.
        /// (static readonly rather than const, so the compiler does not flag the other branch
        /// as unreachable.)
        /// </summary>
        public static readonly bool SimulateCamera = true;

        /// <summary>
        /// Tried in order. The https name resolves locally to the attached camera; the plain-http
        /// address is Lumea's documented fallback. Only a failure to connect moves to the next -
        /// a timeout does not, because the camera may still be busy with the first request.
        /// </summary>
        public static readonly string[] CameraBaseUrls = { "https://camera.lumeadigital.com", "http://10.76.77.1" };

        /// <summary>Seconds to wait for a capture, which includes the camera's own ML analysis.</summary>
        public const int CaptureTimeoutSeconds = 30;

        /// <summary>The empty placeholder specimen a site's document starts with.</summary>
        public const string PlaceholderTemplate = "GrossPart";

        /// <summary>The Report Builder template inserted once per prostate core.</summary>
        public const string ProstateTemplate = "ProstateBiopsy";

        public const string SiteField = "Site";

        /// <summary>Size is named differently on some templates; the first one present is filled.</summary>
        public static readonly string[] SizeFields = { "Size", "FromSize", "Dimensions" };

        /// <summary>Filled with the number of pieces when the template has it. Blank to skip.</summary>
        public const string FragmentCountField = "FragmentCount";

        /// <summary>
        /// 12-core prostate on two BxChips, one per side, photographed first half then second
        /// half. Listed in LANE order: lane 1 is the lane nearest the chip's arrow cutouts.
        /// Parts are assigned in this order, so it must match how the lab loads the chip AND
        /// how the LIS accessions the parts (first half = A-F, second half = G-L).
        /// </summary>
        public static readonly string[] FirstHalfSites =
        {
            "left lateral base", "left base", "left lateral mid", "left mid", "left lateral apex", "left apex"
        };

        public static readonly string[] SecondHalfSites =
        {
            "right lateral base", "right base", "right lateral mid", "right mid", "right lateral apex", "right apex"
        };

        #endregion SITE CONFIGURATION

        public const int LaneCount = 6;

        /// <summary>One piece of tissue the camera found. All values in mm / mm2.</summary>
        public sealed class Piece
        {
            public double Length;
            public double Width;
            public double Area;
        }

        public sealed class CaptureResult
        {
            public bool Success;
            public string ErrorId;
            public string ErrorMessage;
            public string Serial;
            public bool Simulated;
            /// <summary>chip_v2: always six lanes, each a list of pieces (a lane may be empty).</summary>
            public List<List<Piece>> Lanes = new List<List<Piece>>();
            /// <summary>generic: one entry per piece.</summary>
            public List<Piece> Pieces = new List<Piece>();
        }

        /// <summary>A BxChip capture: six lanes of pieces.</summary>
        public static CaptureResult CaptureChip() => Capture("chip_v2", "cassette");

        /// <summary>A generic tissue-in-cassette capture: a flat list of pieces with width.</summary>
        public static CaptureResult CaptureGeneric() => Capture("generic", "cassette");

        private static CaptureResult Capture(string action, string crop)
        {
            string json;

            if (SimulateCamera)
            {
                StatusLog.WriteWarningEntry($"_LumeaCamera: SIMULATED {action} capture - no camera was called. Set SimulateCamera to false to use a real camera.");
                json = action == "chip_v2" ? SimulatedChipResponse : SimulatedGenericResponse;
            }
            else
            {
                json = Post(action, crop);
            }

            CaptureResult result = Parse(json, action);
            result.Simulated = SimulateCamera;
            return result;
        }

        private static string Post(string action, string crop)
        {
            string body = new JObject { ["action"] = action, ["crop"] = crop }.ToString(Newtonsoft.Json.Formatting.None);
            List<string> failures = new List<string>();

            foreach (string baseUrl in CameraBaseUrls)
            {
                try
                {
                    using (HttpClient client = CreateClient())
                    using (StringContent content = new StringContent(body, Encoding.UTF8, "application/json"))
                    using (HttpResponseMessage response = client.PostAsync(baseUrl.TrimEnd('/') + "/capture", content).Result)
                    {
                        // Error responses (4xx/5xx) carry the same error_id / error_message body, so
                        // they are parsed rather than thrown here.
                        string text = response.Content.ReadAsStringAsync().Result ?? "";
                        StatusLog.WriteInformationEntry($"_LumeaCamera: {baseUrl}/capture ({action}) returned HTTP {(int)response.StatusCode}.");
                        return string.IsNullOrWhiteSpace(text)
                            ? new JObject { ["success"] = false, ["error_id"] = "empty_response", ["error_message"] = $"The camera returned HTTP {(int)response.StatusCode} with no body." }.ToString()
                            : text;
                    }
                }
                catch (AggregateException ex) when (ex.InnerException is HttpRequestException)
                {
                    // Could not connect at all: try the next address.
                    failures.Add($"{baseUrl}: {ex.InnerException.GetBaseException().Message}");
                }
                catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
                {
                    throw new ClientException($"The camera at {baseUrl} did not answer within {CaptureTimeoutSeconds} seconds.");
                }
            }

            throw new ClientException("Could not reach the Lumea camera. Check it is plugged in to this computer. " + string.Join(" | ", failures));
        }

        private static HttpClient CreateClient()
        {
            // The camera's https certificate is only renewed when its weekly update runs. Accepting
            // it regardless keeps capture working on a camera that has missed an update; the
            // address only ever resolves to the camera attached to this machine.
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            HttpClient client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(CaptureTimeoutSeconds) };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static CaptureResult Parse(string json, string action)
        {
            CaptureResult result = new CaptureResult();
            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                result.ErrorId = "unreadable_response";
                result.ErrorMessage = "The camera's response could not be read: " + ex.Message;
                StatusLog.WriteErrorEntry("_LumeaCamera: unreadable response: " + Truncate(json, 500));
                return result;
            }

            // The full response, minus the two base64 images, so the first real capture shows
            // exactly what this camera sends - including anything the spec does not mention.
            JObject forLog = (JObject)root.DeepClone();
            if (forLog["image"] != null) forLog["image"] = "<jpeg omitted>";
            if (forLog["overlay"] != null) forLog["overlay"] = "<png omitted>";
            StatusLog.WriteInformationEntry("_LumeaCamera response: " + forLog.ToString(Newtonsoft.Json.Formatting.None));

            result.Success = root.Value<bool?>("success") ?? false;
            result.ErrorId = root.Value<string>("error_id");
            result.ErrorMessage = root.Value<string>("error_message");
            result.Serial = root.Value<string>("serial");

            JArray measurements = root["measurements"] as JArray;
            if (measurements == null) return result;

            if (action == "chip_v2")
            {
                foreach (JToken lane in measurements)
                    result.Lanes.Add((lane as JArray ?? new JArray()).Select(ToPiece).ToList());
            }
            else
            {
                result.Pieces = measurements.Select(ToPiece).ToList();
            }

            return result;
        }

        private static Piece ToPiece(JToken token) => new Piece
        {
            Length = token.Value<double?>("length") ?? 0,
            Width = token.Value<double?>("width") ?? 0,
            Area = token.Value<double?>("area") ?? 0,
        };

        /// <summary>What to tell the grosser for a failed capture, by the camera's error_id.</summary>
        public static string DescribeError(CaptureResult result)
        {
            switch (result.ErrorId)
            {
                case "capture_in_progress": return "The camera is still processing the last picture. Wait a moment and try again.";
                case "uninitialized_camera": return "The camera hardware was not detected. Check the camera is connected, then try again.";
                case "post_proc_error": return "The picture was taken but could not be measured. Check the chip or cassette is seated properly and try again.";
                case "bad_configuration_settings": return "The camera's factory settings look corrupted. Contact Lumea support.";
                case "bad_action":
                case "bad_crop": return "The camera did not recognise the request. This camera may need updating.";
                default: return string.IsNullOrWhiteSpace(result.ErrorMessage) ? "The camera reported an unknown error." : result.ErrorMessage;
            }
        }

        /// <summary>mm to cm, one decimal: 20.23 -> "2.0".</summary>
        public static string ToCm(double mm) => (mm / 10.0).ToString("0.0", CultureInfo.InvariantCulture);

        // FragmentCount is a picklist, so it is set by answer ExportKey, not free text. Its
        // answers are One..Six (AltData 1..6) and Multiple for anything more.
        private static readonly string[] FragmentCountAnswers = { "One", "Two", "Three", "Four", "Five", "Six" };
        private const string FragmentCountMany = "Multiple";

        /// <summary>The FragmentCount answer ExportKey for a piece count: 3 -> "Three", 7+ -> "Multiple".</summary>
        public static string CountAsWord(int count) =>
            count >= 1 && count <= FragmentCountAnswers.Length ? FragmentCountAnswers[count - 1] : FragmentCountMany;

        private static string Truncate(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...");

        // Shaped exactly like the examples in Lumea's camera spec. The chip one has an empty
        // lane (4) and multi-piece lanes (3 and 6) on purpose, so both paths get exercised.
        private const string SimulatedChipResponse = @"{
            ""success"": true, ""serial"": ""SIMULATED"", ""image"": ""<sim>"", ""overlay"": ""<sim>"", ""px_per_mm"": 20.32,
            ""measurements"": [
                [ { ""area"": 14.2, ""length"": 12.1 } ],
                [ { ""area"": 16.0, ""length"": 14.3 } ],
                [ { ""area"": 5.0, ""length"": 5.2 }, { ""area"": 6.1, ""length"": 6.4 } ],
                [ ],
                [ { ""area"": 17.5, ""length"": 15.8 } ],
                [ { ""area"": 5.1, ""length"": 5.0 }, { ""area"": 6.0, ""length"": 6.2 }, { ""area"": 1.4, ""length"": 1.3 } ]
            ]
        }";

        private const string SimulatedGenericResponse = @"{
            ""success"": true, ""serial"": ""SIMULATED"", ""image"": ""<sim>"", ""overlay"": ""<sim>"", ""px_per_mm"": 20.32,
            ""measurements"": [ { ""area"": 38.4, ""length"": 8.2, ""width"": 6.1 } ]
        }";

#region CLOSEOUT BOILERPLATE
    } // close class
} // close namespace
#endregion CLOSEOUT BOILERPLATE
