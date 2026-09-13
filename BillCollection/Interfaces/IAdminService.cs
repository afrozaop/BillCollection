using BillCollection.Models;

namespace BillCollection.Interfaces
{
    public interface IAdminService
    {
        Task<int> GetBranchCountAsync();
        Task<int> GetUserCountAsync();
        Task<int> GetProviderCountAsync();
        Task<int> GetAssignmentCountAsync();

        Task<List<AppUser>> GetUsersAsync();

        Task<AppUser?> GetUserAsync(long id);

        Task<List<BillProvider>> GetBillProvidersAsync();

        Task<BillProvider?> GetBillProviderAsync(int id);

        Task<bool> CreateBillProviderAsync(
            BillProvider provider);

        Task<bool> UpdateBillProviderAsync(
            int id,
            BillProvider provider);

        Task<bool> DeactivateBillProviderAsync(
            int id);

        Task<List<Branch>> GetActiveBranchesAsync();

        Task<List<BillProvider>> GetActiveProvidersAsync();

        Task<List<int>> GetSelectedProviderIdsAsync(
            int branchId);

        Task<bool> SaveBranchProviderAccessAsync(
            int branchId,
            int[] providerIds,
            string? createdBy);
    }
}