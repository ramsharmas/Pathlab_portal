using System;
using System.Threading;
using System.Web;
using System.Data.Entity;
using PathlabWcfService.Helpers;
using PathlabWcfService.Model;

namespace PathlabWcfService
{
    public class WebApiApplication : HttpApplication
    {
        // Runs ReminderJob.RunOnce() hourly for as long as the app pool is alive.
        // Caveat: an idle IIS app pool can recycle/sleep between requests, which pauses
        // this timer — for guaranteed delivery, wire ReminderJob.RunOnce() into IIS
        // "Application Initialization" keep-alive or an external scheduled task instead.
        private static Timer _reminderTimer;

        // Keeps LabTests.TestCount in step with the LIMS SetupTestsuite list without
        // needing an admin to trigger SyncTestCatalogue manually. No-ops cheaply when
        // LimsBaseUrl isn't configured (see LimsClient.FetchCatalogue).
        private static Timer _catalogueSyncTimer;

        // Recurring-test rebook reminders (Cart & Checkout Phase 4) — see SubscriptionJob.
        private static Timer _subscriptionTimer;

        // CORS preflight fix: the portal (Pathlabfrontend) and this WCF service run
        // on different ports, so every POST call from the browser (CreateBooking,
        // UpdatePaymentStatus, SubmitFeedback, etc.) is preceded by an OPTIONS
        // preflight request. WCF's webHttpBinding has no operation mapped to
        // OPTIONS and was replying 405, which makes the browser block the actual
        // POST — the portal then shows "Unable to connect to service" even though
        // the service is up and GETs work fine. The actual CORS header VALUES
        // still come from Web.config's <httpProtocol> customHeaders (IIS attaches
        // those to every response, including this one) — this just needs to make
        // the preflight itself return 200 instead of reaching WCF and 405'ing.
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
            {
                HttpContext.Current.Response.StatusCode = 200;
                HttpContext.Current.Response.End();
            }
        }

        protected void Application_Start()
        {
            Database.SetInitializer(new PathlabDbInitializer());

            _reminderTimer = new Timer(
                _ => ReminderJob.RunOnce(),
                null,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromHours(1));

            _catalogueSyncTimer = new Timer(
                _ => new PathlabService().SyncTestCatalogue(),
                null,
                TimeSpan.FromMinutes(3),
                TimeSpan.FromHours(6));

            _subscriptionTimer = new Timer(
                _ => SubscriptionJob.RunOnce(),
                null,
                TimeSpan.FromMinutes(4),
                TimeSpan.FromHours(24));
        }
    }

    public class PathlabDbInitializer : CreateDatabaseIfNotExists<PathlabDbContext>
    {
        protected override void Seed(PathlabDbContext db)
        {
            // Seed default lab tests
            db.LabTests.AddRange(new[]
            {
                new LabTest { TestSuiteID="CBC001", TestSuiteName="Complete Blood Count (CBC)", ShortName="CBC", Price=299, SampleType="Blood", TestCount=24, ReportTime="4-6 hrs", Fasting="Not Required", Category="Blood" },
                new LabTest { TestSuiteID="LFT001", TestSuiteName="Liver Function Test (LFT)", ShortName="LFT", Price=499, SampleType="Blood", TestCount=11, ReportTime="Same Day", Fasting="8 hrs", Category="Liver" },
                new LabTest { TestSuiteID="KFT001", TestSuiteName="Kidney Function Test (KFT)", ShortName="KFT", Price=449, SampleType="Blood & Urine", TestCount=8, ReportTime="Same Day", Fasting="8 hrs", Category="Kidney" },
                new LabTest { TestSuiteID="TFT001", TestSuiteName="Thyroid Function Test (TFT)", ShortName="TFT", Price=399, SampleType="Blood", TestCount=3, ReportTime="Same Day", Fasting="Not Required", Category="Thyroid" },
                new LabTest { TestSuiteID="BG001", TestSuiteName="Blood Glucose Fasting", ShortName="FBS", Price=99, SampleType="Blood", TestCount=1, ReportTime="2-4 hrs", Fasting="8 hrs", Category="Diabetes" },
                new LabTest { TestSuiteID="HBA1C001", TestSuiteName="HbA1c (Glycosylated Hemoglobin)", ShortName="HbA1c", Price=349, SampleType="Blood", TestCount=1, ReportTime="Same Day", Fasting="Not Required", Category="Diabetes" },
                new LabTest { TestSuiteID="LIP001", TestSuiteName="Lipid Profile", ShortName="Lipid", Price=349, SampleType="Blood", TestCount=8, ReportTime="Same Day", Fasting="12 hrs", Category="Heart" },
                new LabTest { TestSuiteID="VD001", TestSuiteName="Vitamin D Total", ShortName="Vit D", Price=799, SampleType="Blood", TestCount=1, ReportTime="Same Day", Fasting="Not Required", Category="Vitamins" },
                new LabTest { TestSuiteID="VB001", TestSuiteName="Vitamin B12", ShortName="Vit B12", Price=599, SampleType="Blood", TestCount=1, ReportTime="Same Day", Fasting="Not Required", Category="Vitamins" },
                new LabTest { TestSuiteID="UA001", TestSuiteName="Urine Analysis (Routine)", ShortName="Urine", Price=99, SampleType="Urine", TestCount=18, ReportTime="2-4 hrs", Fasting="Not Required", Category="Urine" }
            });

            db.HealthPackages.AddRange(new[]
            {
                new HealthPackage { PackageCode="BASIC001", PackageName="Basic Health Check", Description="Essential tests for routine health monitoring", Price=599, OriginalPrice=1200, TestCount=35, SampleType="Blood & Urine", ReportTime="Same Day", Fasting="8 hrs", Badge="POPULAR", Includes="CBC,Urine,Blood Glucose,Lipid Profile" },
                new HealthPackage { PackageCode="COMP001", PackageName="Comprehensive Health Check", Description="Complete body health screening", Price=1799, OriginalPrice=4000, TestCount=72, SampleType="Blood & Urine", ReportTime="Same Day", Fasting="12 hrs", Badge="BEST VALUE", Includes="CBC,LFT,KFT,TFT,Lipid,Vitamin D,HbA1c,Urine" },
                new HealthPackage { PackageCode="DIA001", PackageName="Diabetes Care Package", Description="Complete diabetes monitoring panel", Price=699, OriginalPrice=1500, TestCount=18, SampleType="Blood & Urine", ReportTime="Same Day", Fasting="8 hrs", Badge="DIABETIC", Includes="FBS,PPBS,HbA1c,Lipid,KFT,Urine" }
            });

            db.SaveChanges();
            base.Seed(db);
        }
    }
}
