using BillCollection.Models;
using BillCollection.ViewModels;

namespace BillCollection.Interfaces
{
    public interface IBranchService
    {
        Task<Branch?> GetBranchAsync(
            int branchId);

        Task<List<BranchBillProvider>>
            GetAssignedProvidersAsync(
                int branchId);

        /// <summary>Provider-wise summary for the branch dashboard (null = branch not found).</summary>
        Task<BranchDashboardViewModel?>
            GetDashboardAsync(
                int branchId);
    }
}