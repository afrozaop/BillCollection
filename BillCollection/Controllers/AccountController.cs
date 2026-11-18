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

            // First login (or password reset by the administrator): nothing but the
            // Change Password page may be used until the password has been changed.
            // LastLoginDate stays empty until that has happened.
            var mustChangePassword = user.LastLoginDate == null;

            if (mustChangePassword)
            {
                claims.Add(new Claim("MustChangePassword", "true"));
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

            if (mustChangePassword)
            {
                return RedirectToAction(nameof(ChangePassword));
            }

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


        private static readonly HashSet<string> WeakPasswords =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "123456", "1234567", "12345678", "password", "Test@12345", "admin123", "bkbvas"
            };

        // =========================
        // CHANGE PASSWORD - GET
        // =========================

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            ViewBag.MustChange = User.HasClaim("MustChangePassword", "true");
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
            var mustChange = User.HasClaim("MustChangePassword", "true");
            ViewBag.MustChange = mustChange;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "The new password must be different from the current password.");

                return View(model);
            }

            if (WeakPasswords.Contains(model.NewPassword))
            {
                ModelState.AddModelError(
                    "NewPassword",
                    "This password is too common. Please choose another one.");

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

            if (mustChange)
            {
                // The first-login restriction ends here.
                var idText = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (long.TryParse(idText, out var userId))
                {
                    await _accountService.UpdateLastLoginAsync(userId);
                }

                var authResult =
                    await HttpContext.AuthenticateAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);

                var keptClaims =
                    User.Claims
                        .Where(c => c.Type != "MustChangePassword")
                        .ToList();

                var newIdentity =
                    new ClaimsIdentity(
                        keptClaims,
                        CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(newIdentity),
                    authResult?.Properties);

                TempData["SuccessMessage"] =
                    "Password changed successfully. You can continue now.";

                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                return role == "HeadOffice"
                    ? RedirectToAction("Index", "Admin")
                    : RedirectToAction("Index", "Branch");
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