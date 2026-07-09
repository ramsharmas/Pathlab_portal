using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class CareersController : Controller
    {
        public ActionResult Life()
        {
            ViewBag.Title = "Life at Swapnil Diagnostics";
            return View();
        }

        public ActionResult Opportunities()
        {
            ViewBag.Title = "Current Opportunities";
            return View();
        }
    }
}
