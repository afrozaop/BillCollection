using BillCollection.Models;

namespace BillCollection.Interfaces
{
    public interface IAccountService
    {
        Task<AppUser?> ValidateUserAsync(
            string username,
            string password);

        Task<bool> ChangePasswordAsync(
            string username,
            string currentPassword,
            string newPassword);

        Task<bool> ResetPasswordAsync(
            long userId);

        Task UpdateLastLoginAsync(
            long userId);

        bool VerifyPassword(
            string password,
            string storedHash);

        string CreatePasswordHash(
            string password);
    }
}