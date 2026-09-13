using BillCollection.Interfaces;
using BillCollection.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "HeadOffice")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IAccountService _accountService;

        public AdminController(
            IAdminService adminService,
            IAccountService accountService)
        {
            _adminService = adminService;
            _accountService = accountService;
        }

        // =========================================================
        // ADMIN DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var branchCount =
                await _adminService.GetBranchCountAsync();

            var userCount =
                await _adminService.GetUserCountAsync();

            var providerCount =
                await _adminService.GetProviderCountAsync();

            var assignmentCount =
                await _adminService.GetAssignmentCountAsync();

            ViewBag.BranchCount = branchCount;
            ViewBag.UserCount = userCount;
            ViewBag.ProviderCount = providerCount;
            ViewBag.AssignmentCount = assignmentCount;

            return View();
        }

        // =========================================================
        // USERS LIST
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users =
                await _adminService.GetUsersAsync();

            return View(users);
        }

        // =========================================================
        // BILL PROVIDERS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> BillProviders()
        {
            var providers =
                await _adminService.GetBillProvidersAsync();

            return View(providers);
        }

        // =========================================================
        // CREATE BILL PROVIDER - GET
        // =========================================================

        [HttpGet]
        public IActionResult CreateBillProvider()
        {
            return View();
        }

        // =========================================================
        // CREATE BILL PROVIDER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBillProvider(
            BillProvider provider)
        {
            if (!ModelState.IsValid)
            {
                return View(provider);
            }

            var success =
                await _adminService
                    .CreateBillProviderAsync(provider);

            if (!success)
            {
                ModelState.AddModelError(
                    "",
                    "Unable to create bill provider.");

                return View(provider);
            }

            TempData["Success"] =
                "Bill Provider created successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }

        // =========================================================
        // EDIT BILL PROVIDER - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> EditBillProvider(
            int id)
        {
            var provider =
                await _adminService
                    .GetBillProviderAsync(id);

            if (provider == null)
            {
                return NotFound();
            }

            return View(provider);
        }

        // =========================================================
        // EDIT BILL PROVIDER - POST
        // =========================================================

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

            var success =
                await _adminService
                    .UpdateBillProviderAsync(
                        id,
                        provider);

            if (!success)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Bill Provider updated successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }

        // =========================================================
        // DELETE / DEACTIVATE BILL PROVIDER
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteBillProvider(int id)
        {
            var success =
                await _adminService
                    .DeactivateBillProviderAsync(id);

            if (!success)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Bill Provider deactivated successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }

        // =========================================================
        // BRANCH PROVIDER ACCESS - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult>
            BranchProviderAccess(int? branchId)
        {
            var branches =
                await _adminService
                    .GetActiveBranchesAsync();

            var providers =
                await _adminService
                    .GetActiveProvidersAsync();

            var selectedProviderIds =
                new List<int>();

            if (branchId.HasValue)
            {
                selectedProviderIds =
                    await _adminService
                        .GetSelectedProviderIdsAsync(
                            branchId.Value);
            }

            ViewBag.Branches = branches;

            ViewBag.Providers = providers;

            ViewBag.SelectedProviderIds =
                selectedProviderIds;

            ViewBag.SelectedBranchId =
                branchId;

            return View();
        }

        // =========================================================
        // BRANCH PROVIDER ACCESS - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            BranchProviderAccess(
                int branchId,
                int[] providerIds)
        {
            var createdBy =
                User.Identity?.Name;

            var success =
                await _adminService
                    .SaveBranchProviderAccessAsync(
                        branchId,
                        providerIds,
                        createdBy);

            if (!success)
            {
                TempData["Error"] =
                    "Branch not found.";

                return RedirectToAction(
                    nameof(BranchProviderAccess));
            }

            TempData["Success"] =
                "Branch provider access updated successfully.";

            return RedirectToAction(
                nameof(BranchProviderAccess));
        }

        // =========================================================
        // RESET PASSWORD - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult>
            ResetPassword(long id)
        {
            var user =
                await _adminService.GetUserAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // =========================================================
        // RESET PASSWORD - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ResetPasswordConfirm(long id)
        {
            var user =
                await _adminService.GetUserAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var success =
                await _accountService
                    .ResetPasswordAsync(id);

            if (!success)
            {
                TempData["Error"] =
                    "Password reset failed.";

                return RedirectToAction(
                    nameof(Users));
            }

            TempData["Success"] =
                $"Password for {user.Username} " +
                "has been reset successfully.";

            return RedirectToAction(
                nameof(Users));
        }
    }
}