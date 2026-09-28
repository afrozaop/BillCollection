using BillCollection.Models;

namespace BillCollection.Interfaces
{
    public interface IAdminService
    {
        // =========================================================
        // DASHBOARD
        // =========================================================

        Task<int> GetBranchCountAsync();

        Task<int> GetUserCountAsync();

        Task<int> GetProviderCountAsync();

        Task<int> GetAssignmentCountAsync();


        // =========================================================
        // USERS
        // =========================================================

        Task<(List<AppUser> Users, int TotalRecords)> GetUsersAsync(
            string? search,
            int page,
            int pageSize);

        Task<AppUser?> GetUserAsync(long id);

        Task<bool> UsernameExistsAsync(string username);

        Task CreateUserAsync(
            AppUser user,
            string password,
            string createdBy);


        // =========================================================
        // BILL PROVIDERS
        // =========================================================

        Task<List<BillProvider>> GetBillProvidersAsync();

        Task<BillProvider?> GetBillProviderAsync(int id);

        Task<bool> CreateBillProviderAsync(
            BillProvider provider);

        Task<bool> UpdateBillProviderAsync(
            int id,
            BillProvider provider);

        Task DeleteBillProviderAsync(int id);

        Task DeactivateBillProviderAsync(int id);

        Task ActivateBillProviderAsync(int id);


        // =========================================================
        // BRANCHES
        // =========================================================

        Task<List<Branch>> GetActiveBranchesAsync();


        // =========================================================
        // ACTIVE BILL PROVIDERS
        // =========================================================

        Task<List<BillProvider>> GetActiveProvidersAsync();


        // =========================================================
        // BRANCH PROVIDER ACCESS
        // =========================================================

        Task<List<int>> GetSelectedProviderIdsAsync(
            int branchId);

        Task<bool> SaveBranchProviderAccessAsync(
            int branchId,
            int[] providerIds,
            string? createdBy);
        Task ResetUserPasswordAsync(long id, string newPassword);
    }
}