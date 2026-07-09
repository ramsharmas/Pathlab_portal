using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class PartnerController : Controller
    {
        public ActionResult Franchise()
        {
            ViewBag.Title = "Pathlab (Franchise)";
            return View();
        }

        public ActionResult HospitalLabManagement()
        {
            ViewBag.Title = "Hospital Laboratory Management";
            return View();
        }

        public ActionResult RetailLabManagement()
        {
            ViewBag.Title = "Retail Laboratory Management";
            return View();
        }

        public ActionResult PathlabPlus()
        {
            ViewBag.Title = "Pathlab Plus";
            return View();
        }
    }
}
