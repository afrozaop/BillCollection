using BillCollection.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "HeadOffice")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // ADMIN DASHBOARD
        // =========================================================

        public async Task<IActionResult> Index()
        {
            var branchCount = await _context.Branches
                .CountAsync();

            var userCount = await _context.AppUsers
                .CountAsync();

            var providerCount = await _context.BillProviders
                .CountAsync();

            var assignmentCount = await _context.BranchBillProviders
                .CountAsync();

            ViewBag.BranchCount = branchCount;
            ViewBag.UserCount = userCount;
            ViewBag.ProviderCount = providerCount;
            ViewBag.AssignmentCount = assignmentCount;

            return View();
        }


        // =========================================================
        // BILL PROVIDERS
        // =========================================================

        // GET: /Admin/BillProviders
        [HttpGet]
        public async Task<IActionResult> BillProviders()
        {
            var providers = await _context.BillProviders
                .AsNoTracking()
                .OrderBy(x => x.ProviderName)
                .ToListAsync();

            return View(providers);
        }


        // =========================================================
        // CREATE BILL PROVIDER
        // =========================================================

        // GET: /Admin/CreateBillProvider
        [HttpGet]
        public IActionResult CreateBillProvider()
        {
            return View();
        }


        // POST: /Admin/CreateBillProvider
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBillProvider(
            BillProvider provider)
        {
            if (!ModelState.IsValid)
            {
                return View(provider);
            }

            provider.CreatedDate = DateTime.UtcNow;
            provider.IsActive = true;

            _context.BillProviders.Add(provider);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Bill Provider created successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }


        // =========================================================
        // EDIT BILL PROVIDER
        // =========================================================

        // GET: /Admin/EditBillProvider/5
        [HttpGet]
        public async Task<IActionResult> EditBillProvider(int id)
        {
            var provider = await _context.BillProviders
                .FindAsync(id);

            if (provider == null)
            {
                return NotFound();
            }

            return View(provider);
        }


        // POST: /Admin/EditBillProvider/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBillProvider(
            int id,
            BillProvider provider)
        {
            if (id != provider.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(provider);
            }

            var existing = await _context.BillProviders
                .FindAsync(id);

            if (existing == null)
            {
                return NotFound();
            }

            existing.ProviderCode =
                provider.ProviderCode;

            existing.ProviderName =
                provider.ProviderName;

            existing.IsActive =
                provider.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Bill Provider updated successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }


        // =========================================================
        // DELETE / DEACTIVATE BILL PROVIDER
        // =========================================================

        // POST: /Admin/DeleteBillProvider/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBillProvider(int id)
        {
            var provider = await _context.BillProviders
                .FindAsync(id);

            if (provider == null)
            {
                return NotFound();
            }

            // Soft delete
            provider.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Bill Provider deactivated successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }


        // =========================================================
        // BRANCH PROVIDER ACCESS
        // =========================================================

        // GET:
        // /Admin/BranchProviderAccess
        //
        // /Admin/BranchProviderAccess?branchId=1
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> BranchProviderAccess(
            int? branchId)
        {
            // -----------------------------------------------------
            // ACTIVE BRANCHES
            // -----------------------------------------------------

            var branches = await _context.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.BranchCode)
                .ToListAsync();


            // -----------------------------------------------------
            // ACTIVE BILL PROVIDERS
            // -----------------------------------------------------

            var providers = await _context.BillProviders
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.ProviderName)
                .ToListAsync();


            // -----------------------------------------------------
            // SELECTED PROVIDER IDS
            // -----------------------------------------------------

            var selectedProviderIds =
                new List<int>();


            if (branchId.HasValue)
            {
                selectedProviderIds =
                    await _context.BranchBillProviders
                        .AsNoTracking()
                        .Where(x =>
                            x.BranchId == branchId.Value &&
                            x.IsActive)
                        .Select(x => x.ProviderId)
                        .Distinct()
                        .ToListAsync();
            }


            // -----------------------------------------------------
            // SEND DATA TO VIEW
            // -----------------------------------------------------

            ViewBag.Branches =
                branches;

            ViewBag.Providers =
                providers;

            ViewBag.SelectedProviderIds =
                selectedProviderIds;

            ViewBag.SelectedBranchId =
                branchId;


            return View();
        }


        // =========================================================
        // SAVE BRANCH PROVIDER ACCESS
        // =========================================================

        // POST:
        // /Admin/BranchProviderAccess
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchProviderAccess(
     int branchId,
     int[] providerIds)
        {
            // Check Branch
            var branch = await _context.Branches
                .FirstOrDefaultAsync(x =>
                    x.Id == branchId &&
                    x.IsActive);

            if (branch == null)
            {
                TempData["Error"] = "Branch not found.";

                return RedirectToAction(
                    nameof(BranchProviderAccess));
            }

            // Selected Provider IDs
            var selectedIds = providerIds?
                .Distinct()
                .ToList()
                ?? new List<int>();


            // Existing assignments
            var existing = await _context.BranchBillProviders
                .Where(x => x.BranchId == branchId)
                .ToListAsync();


            // Remove old assignments
            if (existing.Any())
            {
                _context.BranchBillProviders.RemoveRange(existing);
            }


            // Add new assignments
            if (selectedIds.Any())
            {
                var validProviderIds =
                    await _context.BillProviders
                        .Where(x =>
                            x.IsActive &&
                            selectedIds.Contains(x.Id))
                        .Select(x => x.Id)
                        .Distinct()
                        .ToListAsync();

                foreach (var providerId in validProviderIds)
                {
                    _context.BranchBillProviders.Add(
                        new BranchBillProvider
                        {
                            BranchId = branchId,
                            ProviderId = providerId,
                            IsActive = true,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = User.Identity?.Name
                        });
                }
            }


            // Save
            await _context.SaveChangesAsync();


            // Success message
            TempData["Success"] =
                "Branch provider access updated successfully.";


            // IMPORTANT:
            // Go back to first page.
            // No branch will be selected.
            return RedirectToAction(
                nameof(BranchProviderAccess));
        }
    }
}