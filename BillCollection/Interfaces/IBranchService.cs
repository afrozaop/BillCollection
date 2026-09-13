using BillCollection.Models;

namespace BillCollection.Interfaces
{
    public interface IBranchService
    {
        Task<Branch?> GetBranchAsync(
            int branchId);

        Task<List<BranchBillProvider>>
            GetAssignedProvidersAsync(
                int branchId);
    }
}