using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login()
        {
            ViewBag.Title = "Login";
            return View();
        }
    }
}
