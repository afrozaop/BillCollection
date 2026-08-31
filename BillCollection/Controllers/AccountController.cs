using BillCollection.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BillCollection.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
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
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = model.Username.Trim();

            // Find user
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x =>
                    x.Username == username &&
                    x.IsActive);

            // DEBUG 1
            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "DEBUG: USER NOT FOUND");

                return View(model);
            }

            // DEBUG 2
            if (!VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(
                    "",
                    "DEBUG: PASSWORD VERIFY FAILED");

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

            if (user.BranchId.HasValue)
            {
                claims.Add(new Claim(
                    "BranchId",
                    user.BranchId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            user.LastLoginDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            ModelState.AddModelError(
                "",
                "Invalid user role.");

            return View(model);
        }
        [AllowAnonymous]
        public IActionResult TestPassword()
        {
            string newHash = CreatePasswordHash("Test@12345");

            return Content(newHash);
        }
     
        // =========================
        // CREATE PASSWORD HASH
        // =========================

        private static string CreatePasswordHash(string password)
        {
            int iterations = 100000;

            byte[] salt = RandomNumberGenerator.GetBytes(16);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256);

            byte[] hash = pbkdf2.GetBytes(32);

            return $"{iterations}." +
                   $"{Convert.ToBase64String(salt)}." +
                   $"{Convert.ToBase64String(hash)}";
        }


        // =========================
        // VERIFY PASSWORD
        // =========================

        private static bool VerifyPassword(
            string password,
            string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            var parts = storedHash.Split('.');

            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], out int iterations))
                return false;

            try
            {
                byte[] salt =
                    Convert.FromBase64String(parts[1]);

                byte[] expectedHash =
                    Convert.FromBase64String(parts[2]);

                using var pbkdf2 =
                    new Rfc2898DeriveBytes(
                        password,
                        salt,
                        iterations,
                        HashAlgorithmName.SHA256);

                byte[] actualHash =
                    pbkdf2.GetBytes(expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedHash);
            }
            catch
            {
                return false;
            }
        }
        // =========================
        // LOGOUT
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
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