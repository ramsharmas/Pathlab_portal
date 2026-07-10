using System.Configuration;
using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class AdminController : Controller
    {
        // ── PIN Gate ─────────────────────────────────────────────────────────
        // Set AdminPin in Web.config appSettings to your preferred PIN.
        // Until it is set the default is "1234".  Never store production PINs
        // in source control — override via environment-specific Web.config transforms
        // or IIS application settings.
        private static string ConfiguredPin =>
            ConfigurationManager.AppSettings["AdminPin"] ?? "1234";

        private bool IsAuthenticated =>
            Session["AdminAuthenticated"] as bool? == true;

        // GET /Admin/Dashboard — show login overlay if not authed
        public ActionResult Dashboard()
        {
            ViewBag.Title = "Admin Dashboard";
            ViewBag.AdminAuthed = IsAuthenticated;
            // Only handed to the browser once the PIN gate has passed — this is what
            // lets the Admin panel's AngularJS calls authenticate to the WCF
            // service's admin-only endpoints (GetAdminStats, GetAllPatients, etc).
            ViewBag.AdminApiKey = IsAuthenticated ? ConfigurationManager.AppSettings["AdminApiKey"] : null;
            return View();
        }

        // POST /Admin/Login — validate PIN, set session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string pin, string returnUrl)
        {
            if (pin == ConfiguredPin)
            {
                Session["AdminAuthenticated"] = true;
                return RedirectToAction("Dashboard");
            }
            TempData["LoginError"] = "Incorrect PIN. Please try again.";
            return RedirectToAction("Dashboard");
        }

        // POST /Admin/Logout — clear session
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("AdminAuthenticated");
            return RedirectToAction("Dashboard");
        }
    }
}

