using System.Web.Mvc;

namespace Pathlabfrontend.Controllers
{
    public class BookingController : Controller
    {
        public ActionResult Checkout()
        {
            ViewBag.Title = "Checkout";
            return View();
        }

        public ActionResult Cart()
        {
            ViewBag.Title = "Your Cart";
            return View();
        }

        public ActionResult ChooseCollection()
        {
            ViewBag.Title = "Choose Collection";
            return View();
        }

        public ActionResult OrderReview()
        {
            return RedirectToAction("Checkout");
        }

        public ActionResult Payment()
        {
            return RedirectToAction("Checkout");
        }

        public ActionResult Confirmation()
        {
            return RedirectToAction("Portal", "Patient", new { tab = "bookings" });
        }
    }
}
