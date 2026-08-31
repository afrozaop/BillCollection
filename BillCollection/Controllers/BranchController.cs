using BillCollection.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "Branch")]
    public class BranchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BranchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Branch/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Get logged-in user's BranchId
            var branchIdValue = User.FindFirst("BranchId")?.Value;

            if (!int.TryParse(branchIdValue, out int branchId))
            {
                return Forbid();
            }

            // Get current branch
            var branch = await _context.Branches
                .FirstOrDefaultAsync(x =>
                    x.Id == branchId &&
                    x.IsActive);

            if (branch == null)
            {
                return NotFound("Branch not found.");
            }

            // Get providers assigned to this branch
            var providers = await _context.BranchBillProviders
                .Include(x => x.Provider)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.IsActive &&
                    x.Provider.IsActive)
                .OrderBy(x => x.Provider.ProviderName)
                .ToListAsync();

            ViewBag.Branch = branch;

            return View(providers);
        }
    }
}