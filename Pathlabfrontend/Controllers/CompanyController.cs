using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class CompanyController : Controller
    {
        public ActionResult About()
        {
            ViewBag.Title = "About Swapnil Diagnostics";
            return View();
        }

        public ActionResult Accreditations()
        {
            ViewBag.Title = "Accreditations";
            return View();
        }

        public ActionResult Management()
        {
            ViewBag.Title = "Management";
            return View();
        }

        public ActionResult Doctors()
        {
            ViewBag.Title = "Our Doctors";
            return View();
        }

        public ActionResult WhyUs()
        {
            ViewBag.Title = "Why Swapnil Diagnostics";
            return View();
        }

        public ActionResult Network()
        {
            ViewBag.Title = "Network";
            return View();
        }

        public ActionResult Newsroom()
        {
            ViewBag.Title = "Newsroom";
            return View();
        }

        public ActionResult Compliance()
        {
            ViewBag.Title = "Compliance";
            return View();
        }

        public ActionResult CentreLocator()
        {
            ViewBag.Title = "Centre / Laboratories Locator";
            return View();
        }
    }
}
