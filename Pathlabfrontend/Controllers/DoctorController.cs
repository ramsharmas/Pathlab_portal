using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class DoctorController : Controller
    {
        public ActionResult PathologyTest()
        {
            ViewBag.Title = "Pathology Tests";
            return View();
        }

        public ActionResult KnowledgeCentre()
        {
            ViewBag.Title = "Knowledge Centre";
            return View();
        }
    }
}
