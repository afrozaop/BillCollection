using BillCollection.Interfaces;
using BillCollection.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BillCollection.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;

        public AccountController(
            IAccountService accountService)
        {
            _accountService = accountService;
        }

        // =========================
        // LOGIN - GET
        // =========================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }


        // =========================
        // LOGIN - POST
        // =========================

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = model.Username.Trim();

            var user =
                await _accountService.ValidateUserAsync(
                    username,
                    model.Password);

            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid username or password.");

                return View(model);
            }

            // =========================
            // CLAIMS
            // =========================

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.Username),

                new Claim(
                    ClaimTypes.GivenName,
                    user.DisplayName),

                new Claim(
                    ClaimTypes.Role,
                    user.Role)
            };

            // =========================
            // BRANCH ID CLAIM
            // =========================

            if (user.BranchId.HasValue)
            {
                claims.Add(
                    new Claim(
                        "BranchId",
                        user.BranchId.Value.ToString()));
            }

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent =
                        model.RememberMe,

                    ExpiresUtc =
                        DateTimeOffset.UtcNow
                            .AddHours(8),

                    AllowRefresh = true
                });

            // =========================
            // UPDATE LAST LOGIN
            // =========================

            await _accountService
                .UpdateLastLoginAsync(user.Id);

            // =========================
            // REDIRECT BY ROLE
            // =========================

            if (user.Role == "HeadOffice")
            {
                return RedirectToAction(
                    "Index",
                    "Admin");
            }

            if (user.Role == "Branch")
            {
                return RedirectToAction(
                    "Index",
                    "Branch");
            }

            // Invalid role
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            ModelState.AddModelError(
                "",
                "Invalid user role.");

            return View(model);
        }


        // =========================
        // CHANGE PASSWORD - GET
        // =========================

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }


        // =========================
        // CHANGE PASSWORD - POST
        // =========================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

                return RedirectToAction(
                    nameof(Login));
            }

            var success =
                await _accountService
                    .ChangePasswordAsync(
                        username,
                        model.CurrentPassword,
                        model.NewPassword);

            if (!success)
            {
                ModelState.AddModelError(
                    "CurrentPassword",
                    "Current password is incorrect.");

                return View(model);
            }

            TempData["SuccessMessage"] =
                "Password changed successfully.";

            return RedirectToAction(
                nameof(ChangePassword));
        }


        // =========================
        // LOGOUT
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme);

            return RedirectToAction(
                nameof(Login));
        }


        // =========================
        // ACCESS DENIED
        // =========================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}