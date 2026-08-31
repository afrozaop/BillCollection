using BillCollection.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var admin = _context.AppUsers
                .FirstOrDefault(x =>
                    x.Username == "hoadmin" &&
                    x.Role == "HeadOffice" &&
                    x.IsActive);

            if (admin == null)
                return Content("Head Office Admin not found.");

            return Content(
                $"Admin Found!\n" +
                $"Id: {admin.Id}\n" +
                $"Username: {admin.Username}\n" +
                $"DisplayName: {admin.DisplayName}\n" +
                $"Role: {admin.Role}"
            );
        
    }
    }
}