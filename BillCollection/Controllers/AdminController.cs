using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.Models.ViewModels;
using BillCollection.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BillCollection.Controllers
{
    [Authorize(Roles = "HeadOffice")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdminService _adminService;
        private readonly IAccountService _accountService;

        public AdminController(
            ApplicationDbContext context,
            IAdminService adminService,
            IAccountService accountService)
        {
            _context = context;
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
        // Search + Pagination
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Users(
            string? search,
            int page = 1,
            int pageSize = 25)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 25;
            }

            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var result =
                await _adminService.GetUsersAsync(
                    search,
                    page,
                    pageSize);

            var model = new UserListViewModel
            {
                Users = result.Users,
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalRecords = result.TotalRecords
            };

            return View(model);
        }


        // =========================================================
        // CREATE USER - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var model = new CreateUserViewModel
            {
                Role = "Branch",
                IsActive = true,
                Branches =
                    await _adminService.GetActiveBranchesAsync()
            };

            return View(model);
        }


        // =========================================================
        // CREATE USER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            CreateUserViewModel model)
        {
            // -----------------------------------------------------
            // Username validation
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(model.Username))
            {
                var usernameExists =
                    await _adminService.UsernameExistsAsync(
                        model.Username.Trim());

                if (usernameExists)
                {
                    ModelState.AddModelError(
                        "Username",
                        "This username already exists.");
                }
            }


            // -----------------------------------------------------
            // Branch validation
            // -----------------------------------------------------

            if (model.Role == "Branch" &&
                !model.BranchId.HasValue)
            {
                ModelState.AddModelError(
                    "BranchId",
                    "Please select a branch.");
            }


            // -----------------------------------------------------
            // If validation fails
            // -----------------------------------------------------

            if (!ModelState.IsValid)
            {
                model.Branches =
                    await _adminService
                        .GetActiveBranchesAsync();

                return View(model);
            }


            // -----------------------------------------------------
            // Get selected branch
            // -----------------------------------------------------

            string? branchRoutingNumber = null;

            if (model.Role == "Branch")
            {
                var branches =
                    await _adminService
                        .GetActiveBranchesAsync();

                var branch =
                    branches.FirstOrDefault(
                        x => x.Id == model.BranchId!.Value);

                if (branch == null)
                {
                    ModelState.AddModelError(
                        "BranchId",
                        "Selected branch is invalid.");

                    model.Branches = branches;

                    return View(model);
                }

                branchRoutingNumber =
                    branch.RoutingNumber;
            }


            // -----------------------------------------------------
            // Create AppUser object
            // -----------------------------------------------------

            var user = new AppUser
            {
                Username =
                    model.Username.Trim(),

                DisplayName =
                    model.DisplayName.Trim(),

                Role =
                    model.Role,

                BranchId =
                    model.Role == "Branch"
                        ? model.BranchId
                        : null,

                BranchRoutingNumber =
                    model.Role == "Branch"
                        ? branchRoutingNumber
                        : null,

                Email =
                    string.IsNullOrWhiteSpace(model.Email)
                        ? null
                        : model.Email.Trim(),

                MobileNumber =
                    string.IsNullOrWhiteSpace(model.MobileNumber)
                        ? null
                        : model.MobileNumber.Trim(),

                Designation =
                    string.IsNullOrWhiteSpace(model.Designation)
                        ? null
                        : model.Designation.Trim(),

                EmployeeId =
                    string.IsNullOrWhiteSpace(model.EmployeeId)
                        ? null
                        : model.EmployeeId.Trim(),

                CanEditCbsDetails =
                    model.CanEditCbsDetails,

                IsActive =
                    model.IsActive
            };


            // -----------------------------------------------------
            // Created By
            // -----------------------------------------------------

            var createdBy =
                User.Identity?.Name
                ?? "HeadOffice";


            // -----------------------------------------------------
            // Save User
            // Password will be hashed inside AdminService
            // -----------------------------------------------------

            await _adminService.CreateUserAsync(
                user,
                model.Password,
                createdBy);


            // -----------------------------------------------------
            // Success
            // -----------------------------------------------------

            TempData["Success"] =
                "User created successfully.";


            return RedirectToAction(
                nameof(Users));
        }


        // =========================================================
        // BILL PROVIDERS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> BillProviders()
        {
            var providers =
                await _adminService
                    .GetBillProvidersAsync();

            return View(providers);
        }


        // =========================================================
        // DELETE BILL PROVIDER
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBillProvider(
            int id)
        {
            await _adminService
                .DeleteBillProviderAsync(id);

            return RedirectToAction(
                nameof(BillProviders));
        }


        // =========================================================
        // DEACTIVATE BILL PROVIDER
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateBillProvider(
            int id)
        {
            await _adminService
                .DeactivateBillProviderAsync(id);

            return RedirectToAction(
                nameof(BillProviders));
        }


        // =========================================================
        // ACTIVATE BILL PROVIDER
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateBillProvider(
            int id)
        {
            await _adminService
                .ActivateBillProviderAsync(id);

            return RedirectToAction(
                nameof(BillProviders));
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
        // BRANCH PROVIDER ACCESS - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> BranchProviderAccess(
            int? branchId)
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
        public async Task<IActionResult> BranchProviderAccess(
            int branchId,
            int[] providerIds)
        {
            var createdBy =
                User.Identity?.Name;

            providerIds ??=
                Array.Empty<int>();

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
                    nameof(BranchProviderAccess),
                    new { branchId });
            }

            TempData["Success"] =
                "Branch provider access updated successfully.";

            return RedirectToAction(
                nameof(BranchProviderAccess),
                new { branchId });
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
                ModelState.AddModelError(
                    "",
                    "Unable to update bill provider.");

                return View(provider);
            }

            TempData["Success"] =
                "Bill Provider updated successfully.";

            return RedirectToAction(
                nameof(BillProviders));
        }

        // =========================================================
        // RESET BRANCH USER PASSWORD
        // =========================================================

        // GET:
        // /Admin/ResetPassword/1050

        [HttpGet]
        public async Task<IActionResult> ResetPassword(long id)
        {
            var user = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }


        // =========================================================
        // CONFIRM RESET PASSWORD
        // =========================================================

        // POST:
        // /Admin/ResetPasswordConfirm/1050

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordConfirm(long id)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // DEFAULT PASSWORD
            // -----------------------------------------------------

            const string defaultPassword =
                "Test@12345";


            // -----------------------------------------------------
            // USE SAME HASH METHOD AS ACCOUNT SERVICE
            // -----------------------------------------------------

            user.PasswordHash =
                _accountService.CreatePasswordHash(
                    defaultPassword);


            // -----------------------------------------------------
            // SAVE
            // -----------------------------------------------------

            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // SUCCESS MESSAGE
            // -----------------------------------------------------

            TempData["Success"] =
                $"Password for {user.Username} has been reset successfully.";


            // -----------------------------------------------------
            // BACK TO USERS
            // -----------------------------------------------------

            return RedirectToAction(
                nameof(Users));
        }

        // =========================================================
        // CREATE PBKDF2 PASSWORD HASH
        // =========================================================

        private string CreatePasswordHash(string password)
        {
            const int iterations = 100000;

            using var rng =
                RandomNumberGenerator.Create();

            byte[] salt = new byte[16];

            rng.GetBytes(salt);


            using var pbkdf2 =
                new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256);


            byte[] hash =
                pbkdf2.GetBytes(32);


            return
                $"{iterations}." +
                $"{Convert.ToBase64String(salt)}." +
                $"{Convert.ToBase64String(hash)}";
        }
    }
}