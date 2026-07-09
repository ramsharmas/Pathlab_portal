using System;
using System.Linq;
using PathlabWcfService.Model;

namespace PathlabWcfService.Helpers
{
    // Recurring-test reminders (Cart & Checkout Phase 4). Runs daily (see
    // Global.asax) and SMS's "time to rebook" once a TestSubscription's
    // NextDueDate arrives, then pushes NextDueDate forward by FrequencyDays so
    // it doesn't repeat tomorrow. This schedules the reminder only — it does NOT
    // auto-charge; a real "subscription" with recurring billing needs a payment
    // gateway mandate (e.g. Razorpay Subscriptions/UPI Autopay), not built here.
    public static class SubscriptionJob
    {
        public static void RunOnce()
        {
            try
            {
                using (var db = new PathlabDbContext())
                {
                    var due = db.TestSubscriptions.Include("Patient")
                        .Where(s => s.IsActive && s.NextDueDate <= DateTime.Today)
                        .ToList();

                    foreach (var s in due)
                    {
                        if (s.Patient == null) { s.NextDueDate = DateTime.Today.AddDays(s.FrequencyDays); continue; }

                        var pref = db.NotificationPreferences.FirstOrDefault(p =>
                            p.PatientId == s.PatientId && p.Channel == "SMS" && p.Type == "Reminder");
                        if (pref == null || pref.Enabled)
                        {
                            SmsHelper.Send(s.Patient.Phone,
                                "It's time to rebook " + s.TestSuiteName + " (every " + s.FrequencyDays +
                                " days). Login to your Patient Portal to book now. - Swapnil Diagnostics",
                                "Reminder", null);
                        }
                        s.NextDueDate = DateTime.Today.AddDays(s.FrequencyDays);
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[SubscriptionJob] run failed: " + ex.Message);
            }
        }
    }
}
