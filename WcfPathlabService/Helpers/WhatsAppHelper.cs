using System;
using System.Configuration;
using System.Net;
using System.Text;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // WhatsApp Business Cloud API (Meta Graph API) sender. Mirrors SmsHelper /
    // EmailHelper: unconfigured means every send is skipped and logged, never
    // faked. Sends TEMPLATE messages only, never free-form text — these
    // notifications are business-initiated, and WhatsApp only allows free-form
    // replies within a 24h window after the *customer* messages first. A real
    // booking-confirmation/report-ready alert requires a template pre-approved
    // in Meta Business Manager, named per event type via
    // WhatsAppTemplate_<Type> (e.g. WhatsAppTemplate_BookingConfirmation).
    public static class WhatsAppHelper
    {
        public static void Send(string phone, string type, string[] bodyParams, string previewMessage, string bookingRef = null)
        {
            string token = ConfigurationManager.AppSettings["WhatsAppApiToken"];
            string phoneNumberId = ConfigurationManager.AppSettings["WhatsAppPhoneNumberId"];
            string templateName = ConfigurationManager.AppSettings["WhatsAppTemplate_" + type];
            string languageCode = ConfigurationManager.AppSettings["WhatsAppLanguageCode"];
            if (string.IsNullOrWhiteSpace(languageCode)) languageCode = "en";
            string countryCode = ConfigurationManager.AppSettings["WhatsAppCountryCode"];
            if (string.IsNullOrWhiteSpace(countryCode)) countryCode = "91";

            bool success;
            string error = null;

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(phone))
            {
                success = false;
                error = string.IsNullOrWhiteSpace(phone)
                    ? "No recipient phone."
                    : "WhatsAppApiToken/WhatsAppPhoneNumberId not configured — skipped.";
                System.Diagnostics.Trace.TraceWarning("[WhatsApp] " + error + " to " + phone + ": " + previewMessage);
            }
            else if (string.IsNullOrWhiteSpace(templateName))
            {
                success = false;
                error = "No WhatsAppTemplate_" + type + " configured — skipped (needs a pre-approved Meta template name for this event type).";
                System.Diagnostics.Trace.TraceWarning("[WhatsApp] " + error);
            }
            else
            {
                try
                {
                    string apiVersion = ConfigurationManager.AppSettings["WhatsAppApiVersion"];
                    if (string.IsNullOrWhiteSpace(apiVersion)) apiVersion = "v19.0";
                    string to = countryCode + phone.TrimStart('0');

                    var paramsJson = new StringBuilder();
                    for (int i = 0; i < (bodyParams?.Length ?? 0); i++)
                    {
                        if (i > 0) paramsJson.Append(",");
                        paramsJson.Append("{\"type\":\"text\",\"text\":").Append(J(bodyParams[i])).Append("}");
                    }

                    string payload =
                        "{\"messaging_product\":\"whatsapp\",\"to\":" + J(to) +
                        ",\"type\":\"template\",\"template\":{\"name\":" + J(templateName) +
                        ",\"language\":{\"code\":" + J(languageCode) + "}" +
                        ",\"components\":[{\"type\":\"body\",\"parameters\":[" + paramsJson + "]}]}}";

                    using (var client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.ContentType] = "application/json";
                        client.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
                        client.UploadString("https://graph.facebook.com/" + apiVersion + "/" + phoneNumberId + "/messages", "POST", payload);
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                    System.Diagnostics.Trace.TraceError("[WhatsApp] Send failed to " + phone + ": " + ex.Message);
                }
            }

            LogNotification(phone, previewMessage, type, bookingRef, success, error);
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

        private static void LogNotification(string phone, string message, string type, string bookingRef, bool success, string error)
        {
            try
            {
                using (var db = new PathlabDbContext())
                {
                    db.NotificationLogs.Add(new NotificationLog
                    {
                        Phone = phone,
                        Channel = "WhatsApp",
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
                System.Diagnostics.Trace.TraceError("[WhatsApp] Failed to write NotificationLog: " + ex.Message);
            }
        }
    }
}
