using System;
using System.Configuration;
using System.Net;
using System.Text;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    public static class SmsHelper
    {
        // type/bookingRef are optional so existing 2-arg call sites keep compiling;
        // pass them to get a meaningful entry in NotificationLogs (e.g. "BookingConfirmation").
        public static void Send(string phone, string message, string type = "General", string bookingRef = null)
        {
            string apiKey = ConfigurationManager.AppSettings["Fast2SmsApiKey"];
            bool success;
            string error = null;

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(phone))
            {
                success = false;
                error = "Fast2SmsApiKey not configured — skipped.";
                System.Diagnostics.Trace.TraceWarning("[SMS] " + error + " to " + phone + ": " + message);
            }
            else
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Headers.Add("authorization", apiKey);
                        client.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
                        var data = new System.Collections.Specialized.NameValueCollection
                        {
                            { "route", "q" },
                            { "message", message },
                            { "language", "english" },
                            { "flash", "0" },
                            { "numbers", phone }
                        };
                        client.UploadValues("https://www.fast2sms.com/dev/bulkV2", "POST", data);
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                    System.Diagnostics.Trace.TraceError("[SMS] Fast2SMS send failed for " + phone + ": " + ex.Message);
                }
            }

            LogNotification(phone, message, type, bookingRef, success, error);
        }

        private static void LogNotification(string phone, string message, string type, string bookingRef, bool success, string error)
        {
            try
            {
                using (var db = new PathlabDbContext())
                {
                    db.NotificationLogs.Add(new NotificationLog
                    {
                        Phone = phone,
                        Channel = "SMS",
                        Type = type,
                        BookingRef = bookingRef,
                        Message = (message ?? "").Length > 500 ? message.Substring(0, 500) : message,
                        Success = success,
                        ErrorDetail = error,
                        CreatedAt = DateTime.Now
                    });
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // Logging must never break the calling flow (booking/status/cancel).
                System.Diagnostics.Trace.TraceError("[SMS] Failed to write NotificationLog: " + ex.Message);
            }
        }
    }
}
