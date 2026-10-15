using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;

namespace BillCollection.Interfaces
{
    public interface IBREBService
    {
        Task<BREBTokenResponse?> AuthenticateAsync(
            CancellationToken cancellationToken = default);

        Task<BREBBillingInfoResponse?> GetBillingInfoAsync(
            int branchId,
            BREBBillingInfoRequest request,
            CancellationToken cancellationToken = default);
        Task<BREBPaymentSubmitResponse?> SubmitPaymentAsync(
            int branchId,
            int transactionId,
            CancellationToken cancellationToken = default);
        Task<int> SaveBillingTransactionAsync(
            int branchId,
            BREBBillingInfoResponse billingInfo,
            string? username,
            CancellationToken cancellationToken = default);
        Task<bool> AcknowledgePaymentAsync(
    int branchId,
    int transactionId,
    string? username,
    CancellationToken cancellationToken = default);
        Task<List<BREBTransaction>> GetCollectionListAsync(
    int branchId,
    CancellationToken cancellationToken = default);
        Task<List<BREBTransaction>> GetUnsuccessfulListAsync(
    int branchId,
    CancellationToken cancellationToken = default);
        Task<List<BREBTransaction>> GetDisputeListAsync(
    int branchId,
    CancellationToken cancellationToken = default);

        Task<BREBReconcileReportViewModel> GetReconcileReportAsync(
    int branchId,
    CancellationToken cancellationToken = default);

    }

}