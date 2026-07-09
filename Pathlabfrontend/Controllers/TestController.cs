using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class TestController : Controller
    {
        public ActionResult BookTest()
        {
            ViewBag.Title = "Book a Test";
            return View();
        }

        public ActionResult Packages()
        {
            ViewBag.Title = "Health Packages";
            return View();
        }

        public ActionResult PackageDetail(int id)
        {
            ViewBag.Title = "Package Details";
            ViewBag.PackageId = id;
            return View();
        }

        public ActionResult PathlabHealthPackages()
        {
            ViewBag.Title = "Pathlab Health Packages";
            return View();
        }

        public ActionResult HomeCollectionPackages()
        {
            ViewBag.Title = "Home Collection Packages";
            return View();
        }

        public ActionResult CovidPackages()
        {
            ViewBag.Title = "COVID Tests and Packages";
            return View();
        }

        public ActionResult CbcTest()
        {
            ViewBag.Title = "CBC Test";
            return View();
        }
    }
}
