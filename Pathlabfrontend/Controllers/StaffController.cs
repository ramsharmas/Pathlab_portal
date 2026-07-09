using System.Configuration;
using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    // Dedicated, mobile-first phlebotomist-facing controller — separate from
    // AdminController so a field phlebotomist only ever gets a PIN into the
    // collection queue, not the full Admin Dashboard (bookings, patients,
    // audit trail, reconciliation, etc).
    public class StaffController : Controller
    {
        // Set PhlebotomistPin in Web.config appSettings to your preferred PIN.
        // Until it is set the default is "1234" — same fallback pattern as
        // AdminController's AdminPin. Never store production PINs in source control.
        private static string ConfiguredPin =>
            ConfigurationManager.AppSettings["PhlebotomistPin"] ?? "1234";

        private bool IsAuthenticated =>
            Session["StaffAuthenticated"] as bool? == true;

        // GET /Staff/Collection — show PIN gate if not authed
        public ActionResult Collection()
        {
            ViewBag.Title = "Collection Queue";
            ViewBag.StaffAuthed = IsAuthenticated;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string pin)
        {
            if (pin == ConfiguredPin)
            {
                Session["StaffAuthenticated"] = true;
                return RedirectToAction("Collection");
            }
            TempData["LoginError"] = "Incorrect PIN. Please try again.";
            return RedirectToAction("Collection");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("StaffAuthenticated");
            return RedirectToAction("Collection");
        }
    }
}
