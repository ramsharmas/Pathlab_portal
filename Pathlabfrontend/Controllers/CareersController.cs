using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class CareersController : Controller
    {
        public ActionResult Life()
        {
            ViewBag.Title = "Life at PATHLAB Diagnostics";
            return View();
        }

        public ActionResult Opportunities()
        {
            ViewBag.Title = "Current Opportunities";
            return View();
        }
    }
}
