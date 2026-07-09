using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // Bridges portal bookings to the LIMS (labsols LIMS-Pathology).
    // Generates the portal-side Sample ID + barcode at booking time so the
    // patient sees them immediately, and pushes a collection job to the LIMS
    // (Jobs/AddSingleSiteJob). When LimsBaseUrl is not configured or the LIMS
    // is unreachable, the booking stays in LimsSyncStatus "Pending"/"Failed"
    // so it can be re-pushed later — booking creation itself never fails.
    public class LimsCatalogueEntry
    {
        public string TestSuiteName { get; set; }
        public int TestCount { get; set; }
    }

    public static class LimsClient
    {
        public static string GenerateSampleId(long seed)
        {
            return "SMP" + seed.ToString().Substring(3);
        }

        public static string GenerateBarcode(long seed)
        {
            return seed.ToString();
        }

        // Returns the LIMS job id, or null when not configured / call failed.
        public static string CreateCollectionJob(Booking booking, string patientName, string patientPhone)
        {
            string baseUrl = ConfigurationManager.AppSettings["LimsBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[LIMS] LimsBaseUrl not configured — job for " + booking.BookingRef + " left as Pending.");
                return null;
            }

            try
            {
                var tests = (booking.Tests ?? Enumerable.Empty<BookingTest>().ToList())
                    .Select(t => "{\"TestSuiteID\":" + J(t.TestSuiteID) +
                                 ",\"TestSuiteName\":" + J(t.TestSuiteName) +
                                 ",\"SampleType\":" + J(t.SampleType) + "}");

                string payload =
                    "{\"JobSource\":\"PatientPortal\"" +
                    ",\"PortalBookingRef\":" + J(booking.BookingRef) +
                    ",\"SampleId\":" + J(booking.SampleId) +
                    ",\"Barcode\":" + J(booking.Barcode) +
                    ",\"JobType\":" + J(booking.CollectionType == "home" ? "HomeCollection" : "WalkIn") +
                    ",\"PatientName\":" + J(patientName) +
                    ",\"PatientPhone\":" + J(patientPhone) +
                    ",\"Address\":" + J(booking.CollectionType == "home" ? booking.Address : booking.BranchName) +
                    ",\"CollectionDate\":" + J(booking.CollectionDate.HasValue ? booking.CollectionDate.Value.ToString("yyyy-MM-dd") : null) +
                    ",\"TimeSlot\":" + J(booking.TimeSlot) +
                    ",\"Tests\":[" + string.Join(",", tests) + "]}";

                string response = Post(baseUrl.TrimEnd('/') + "/Jobs/AddSingleSiteJob", payload);
                return ExtractId(response, "JobId", "jobId", "JobNo", "Id", "id");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[LIMS] AddSingleSiteJob failed for " + booking.BookingRef + ": " + ex.Message);
                return null;
            }
        }

        // Pushes a newly registered patient into the LIMS user master so the portal
        // identity and the lab identity are linked from day one (mirrors the field
        // mapping documented client-side in lims-api.js toLimsUser()). Returns the
        // LIMS patient/user id, or null when not configured / unreachable.
        public static string CreatePatientRecord(Patient p)
        {
            string baseUrl = ConfigurationManager.AppSettings["LimsBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[LIMS] LimsBaseUrl not configured — patient push for " + p.Phone + " left as Pending.");
                return null;
            }

            try
            {
                var nameParts = (p.FullName ?? "").Trim().Split(new[] { ' ' }, 2);
                string payload =
                    "{\"FirstName\":" + J(nameParts.Length > 0 ? nameParts[0] : "") +
                    ",\"LastName\":" + J(nameParts.Length > 1 ? nameParts[1] : "") +
                    ",\"Phone\":" + J(p.Phone) +
                    ",\"Email\":" + J(p.Email) +
                    ",\"Address\":" + J(string.Join(", ", new[] { p.Address, p.City, p.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s)))) +
                    ",\"Username\":" + J(p.Email ?? p.Phone) +
                    ",\"Position\":\"Patient\"" +
                    ",\"Reference\":\"Patient Portal\"" +
                    ",\"Accounts\":" + J("PAT" + p.PatientId) + "}";

                string response = Post(baseUrl.TrimEnd('/') + "/User/New", payload);
                return ExtractId(response, "UserId", "PatientId", "Id", "id");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[LIMS] User/New failed for " + p.Phone + ": " + ex.Message);
                return null;
            }
        }

        // Best-effort profile-sync push for an already-linked patient. No-op (returns
        // false) until CreatePatientRecord has assigned a LimsPatientId.
        public static bool UpdatePatientRecord(Patient p)
        {
            string baseUrl = ConfigurationManager.AppSettings["LimsBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(p.LimsPatientId)) return false;

            try
            {
                string payload =
                    "{\"Phone\":" + J(p.Phone) +
                    ",\"Email\":" + J(p.Email) +
                    ",\"Address\":" + J(string.Join(", ", new[] { p.Address, p.City, p.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s)))) + "}";
                Post(baseUrl.TrimEnd('/') + "/User/Update/" + Uri.EscapeDataString(p.LimsPatientId), payload);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[LIMS] User/Update failed for patient " + p.PatientId + ": " + ex.Message);
                return false;
            }
        }

        // Notifies the LIMS of a payment status change on an already-synced job so
        // lab-side finance doesn't need separate manual reconciliation.
        public static bool PushPaymentStatus(Booking b)
        {
            string baseUrl = ConfigurationManager.AppSettings["LimsBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(b.LimsJobId)) return false;

            try
            {
                string payload =
                    "{\"JobId\":" + J(b.LimsJobId) +
                    ",\"PaymentStatus\":" + J(b.PaymentStatus) +
                    ",\"PaymentRef\":" + J(b.PaymentRef) +
                    ",\"Amount\":" + b.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                Post(baseUrl.TrimEnd('/') + "/Jobs/UpdatePayment", payload);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[LIMS] Jobs/UpdatePayment failed for " + b.BookingRef + ": " + ex.Message);
                return false;
            }
        }

        // Pulls the current test-suite list from the LIMS (SetupTestsuite) for
        // SyncTestCatalogueFromLims() to merge into LabTests. Only TestSuiteName and
        // TestCount come from the LIMS — Price/Description/TestType stay
        // website-maintained, matching the merge convention already sketched in
        // PathlabDB_Setup.sql. Returns null when not configured / unreachable.
        public static List<LimsCatalogueEntry> FetchCatalogue()
        {
            string baseUrl = ConfigurationManager.AppSettings["LimsBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.Accept] = "application/json";
                    string apiKey = ConfigurationManager.AppSettings["LimsApiKey"];
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        client.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;

                    string response = client.DownloadString(baseUrl.TrimEnd('/') + "/Setup/TestsuiteList");
                    var results = new List<LimsCatalogueEntry>();
                    foreach (Match m in Regex.Matches(response ?? "", "\\{[^{}]*\\}"))
                    {
                        string name = ExtractField(m.Value, "TestSuitename", "TestSuiteName", "Name");
                        string countStr = ExtractField(m.Value, "TestCount", "Count");
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        int count;
                        int.TryParse(countStr, out count);
                        results.Add(new LimsCatalogueEntry { TestSuiteName = name, TestCount = count > 0 ? count : 1 });
                    }
                    return results;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[LIMS] Setup/TestsuiteList failed: " + ex.Message);
                return null;
            }
        }

        private static string Post(string url, string payload)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers[HttpRequestHeader.Accept] = "application/json";
                string apiKey = ConfigurationManager.AppSettings["LimsApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
                return client.UploadString(url, "POST", payload);
            }
        }

        private static string ExtractId(string response, params string[] fieldNames)
        {
            string found = ExtractField(response ?? "", fieldNames);
            if (found != null) return found;
            string bare = (response ?? "").Trim().Trim('"');
            return bare.Length > 0 && bare.Length <= 50 && !bare.Contains("{") ? bare : null;
        }

        private static string ExtractField(string json, params string[] fieldNames)
        {
            foreach (var field in fieldNames)
            {
                var m = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"?([^\",}]+)\"?");
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        private static string J(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
