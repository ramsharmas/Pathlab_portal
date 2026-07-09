using System;
using System.Linq;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // Day-before appointment reminders — runs server-side on a timer (see Global.asax)
    // instead of depending on the patient opening the portal. Idempotent: skips a
    // booking if a "Reminder" SMS was already logged for it today.
    public static class ReminderJob
    {
        public static void RunOnce()
        {
            try
            {
                var tomorrowStart = DateTime.Today.AddDays(1);
                var dayAfter = tomorrowStart.AddDays(1);
                var todayStart = DateTime.Today;

                using (var db = new PathlabDbContext())
                {
                    var due = db.Bookings.Include("Patient")
                        .Where(b => b.CollectionDate != null
                                 && b.CollectionDate >= tomorrowStart && b.CollectionDate < dayAfter
                                 && b.BookingStatus != "Cancelled"
                                 && b.SampleStatus < 1)
                        .ToList();

                    foreach (var b in due)
                    {
                        bool alreadySent = db.NotificationLogs.Any(n =>
                            n.BookingRef == b.BookingRef && n.Type == "Reminder" && n.CreatedAt >= todayStart);
                        if (alreadySent || b.Patient == null) continue;

                        var pref = db.NotificationPreferences.FirstOrDefault(p =>
                            p.PatientId == b.PatientId && p.Channel == "SMS" && p.Type == "Reminder");
                        if (pref != null && !pref.Enabled) continue;

                        string place = b.CollectionType == "home"
                            ? "Our phlebotomist will visit your address."
                            : "Please visit " + (b.BranchName ?? "your selected branch") + ".";

                        SmsHelper.Send(b.Patient.Phone,
                            "Reminder: your sample collection for booking " + b.BookingRef + " is tomorrow at " +
                            (b.TimeSlot ?? "your selected slot") + ". " + place + " - Swapnil Diagnostics",
                            "Reminder", b.BookingRef);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[ReminderJob] run failed: " + ex.Message);
            }
        }
    }
}
