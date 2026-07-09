using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // Mirrors SmsHelper's pattern exactly: no SMTP configured means every send is
    // skipped (never silently pretended) but still logged to NotificationLogs so
    // the workflow (e.g. doctor report sharing) is visibly wired end-to-end.
    public static class EmailHelper
    {
        public static void Send(string toEmail, string subject, string body, string type = "General", string bookingRef = null)
        {
            string host = ConfigurationManager.AppSettings["SmtpHost"];
            bool success;
            string error = null;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(toEmail))
            {
                success = false;
                error = string.IsNullOrWhiteSpace(host) ? "SmtpHost not configured — skipped." : "No recipient email.";
                System.Diagnostics.Trace.TraceWarning("[Email] " + error + " to " + toEmail + ": " + subject);
            }
            else
            {
                try
                {
                    int port;
                    int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out port);
                    if (port == 0) port = 587;
                    string user = ConfigurationManager.AppSettings["SmtpUser"];
                    string pass = ConfigurationManager.AppSettings["SmtpPassword"];
                    string from = ConfigurationManager.AppSettings["SmtpFromAddress"];

                    using (var client = new SmtpClient(host, port))
                    using (var msg = new MailMessage(
                        string.IsNullOrWhiteSpace(from) ? user : from, toEmail, subject, body))
                    {
                        client.EnableSsl = true;
                        if (!string.IsNullOrWhiteSpace(user))
                            client.Credentials = new NetworkCredential(user, pass);
                        client.Send(msg);
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                    System.Diagnostics.Trace.TraceError("[Email] Send failed to " + toEmail + ": " + ex.Message);
                }
            }

            LogNotification(toEmail, subject + ": " + body, type, bookingRef, success, error);
        }

        private static void LogNotification(string email, string message, string type, string bookingRef, bool success, string error)
        {
            try
            {
                using (var db = new PathlabDbContext())
                {
                    db.NotificationLogs.Add(new NotificationLog
                    {
                        Phone = email,
                        Channel = "Email",
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
                System.Diagnostics.Trace.TraceError("[Email] Failed to write NotificationLog: " + ex.Message);
            }
        }
    }
}
