using Microsoft.AspNetCore.Mvc;

namespace BillCollection.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            // User is not logged in
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // Head Office user
            if (User.IsInRole("HeadOffice"))
            {
                return RedirectToAction(
                    "Index",
                    "Admin");
            }

            // Branch user
            if (User.IsInRole("Branch"))
            {
                return RedirectToAction(
                    "Index",
                    "Branch");
            }

            // Unknown / unauthorized role
            return RedirectToAction(
                "AccessDenied",
                "Account");
        }
    }
}