using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class PatientController : Controller
    {
        public ActionResult Portal(string tab = "dashboard")
        {
            ViewBag.Title = "Patient Portal";
            ViewBag.ActiveTab = tab;
            return View();
        }

        public ActionResult TrackSample()
        {
            ViewBag.Title = "Track My Sample";
            return View();
        }

        public ActionResult Disease()
        {
            ViewBag.Title = "Disease / Organ / Habit Wise Tests";
            return View();
        }

        public ActionResult Dos()
        {
            ViewBag.Title = "Directory of Services (DOS)";
            return View();
        }

        public ActionResult Feedback()
        {
            ViewBag.Title = "Feedback";
            return View();
        }

        public ActionResult Faq()
        {
            ViewBag.Title = "FAQs";
            return View();
        }

        public ActionResult DownloadReport()
        {
            return RedirectToAction("Portal", new { tab = "reports" });
        }

        // Public, no-login view-only page for the "Share with doctor" link
        // (Controller.js shareReport() builds /Patient/ViewReport?token=...).
        // Keyed by a random share token (GetBookingByShareToken), not the plain
        // BookingRef, since a sequential booking number is guessable and this
        // page is meant to be safely forwardable to a doctor.
        public ActionResult ViewReport()
        {
            ViewBag.Title = "Shared Report";
            ViewBag.ReportToken = Request.QueryString["token"];
            return View();
        }
    }
}
