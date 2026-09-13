using BillCollection.Interfaces;
using BillCollection.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BillCollection.Services
{
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _context;

        public AccountService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // VALIDATE USER
        // =========================

        public async Task<AppUser?> ValidateUserAsync(
            string username,
            string password)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x =>
                    x.Username == username &&
                    x.IsActive);

            if (user == null)
                return null;

            if (!VerifyPassword(
                    password,
                    user.PasswordHash))
            {
                return null;
            }

            return user;
        }

        // =========================
        // CHANGE PASSWORD
        // =========================

        public async Task<bool> ChangePasswordAsync(
            string username,
            string currentPassword,
            string newPassword)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x =>
                    x.Username == username &&
                    x.IsActive);

            if (user == null)
                return false;

            if (!VerifyPassword(
                    currentPassword,
                    user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash =
                CreatePasswordHash(newPassword);

            await _context.SaveChangesAsync();

            return true;
        }

        // =========================
        // RESET PASSWORD
        // =========================

        public async Task<bool> ResetPasswordAsync(long userId)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return false;
            }

            const string defaultPassword = "Test@12345";

            user.PasswordHash =
                CreatePasswordHash(defaultPassword);

            await _context.SaveChangesAsync();

            return true;
        }

        // =========================
        // LAST LOGIN
        // =========================

        public async Task UpdateLastLoginAsync(long userId)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x =>
                    x.Id == userId);

            if (user == null)
                return;

            user.LastLoginDate =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // =========================
        // CREATE PASSWORD HASH
        // =========================

        public string CreatePasswordHash(
            string password)
        {
            const int iterations = 100000;

            byte[] salt =
                RandomNumberGenerator.GetBytes(16);

            using var pbkdf2 =
                new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256);

            byte[] hash =
                pbkdf2.GetBytes(32);

            return $"{iterations}." +
                   $"{Convert.ToBase64String(salt)}." +
                   $"{Convert.ToBase64String(hash)}";
        }

        // =========================
        // VERIFY PASSWORD
        // =========================

        public bool VerifyPassword(
            string password,
            string storedHash)
        {
            if (string.IsNullOrWhiteSpace(
                    storedHash))
            {
                return false;
            }

            var parts =
                storedHash.Split('.');

            if (parts.Length != 3)
                return false;

            if (!int.TryParse(
                    parts[0],
                    out int iterations))
            {
                return false;
            }

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
                    pbkdf2.GetBytes(
                        expectedHash.Length);

                return CryptographicOperations
                    .FixedTimeEquals(
                        actualHash,
                        expectedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}