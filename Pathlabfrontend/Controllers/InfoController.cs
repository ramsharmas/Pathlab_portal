using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class InfoController : Controller
    {
        public ActionResult Blog()
        {
            ViewBag.Title = "Blog";
            return View();
        }

        public ActionResult BlogPost()
        {
            ViewBag.Title = "Life at PATHLAB Diagnostics";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Title = "Contact Us";
            return View();
        }

        public ActionResult Video()
        {
            ViewBag.Title = "Informative Videos";
            return View();
        }

        public ActionResult Terms()
        {
            ViewBag.Title = "Terms of Use";
            return View();
        }

        public ActionResult Privacy()
        {
            ViewBag.Title = "Privacy Policy";
            return View();
        }

        public ActionResult Cookie()
        {
            ViewBag.Title = "Cookie Policy";
            return View();
        }

        public ActionResult Logistics()
        {
            ViewBag.Title = "Logistics and IT";
            return View();
        }
    }
}
