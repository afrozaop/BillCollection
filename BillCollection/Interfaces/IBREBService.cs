using BillCollection.Models.BREB;
using BillCollection.ViewModels.BREB;

namespace BillCollection.Interfaces
{
    public interface IBREBService
    {
        /// <summary>True when the built-in test accounts are used instead of the real BREB API.</summary>
        bool IsMockMode { get; }

        Task<BREBBillingInfoResponse?> GetBillingInfoAsync(
            int branchId, BREBBillingInfoRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// One-click flow: uses the bill that was just queried (no second billinginfo call),
        /// saves the transaction, submits the payment and acknowledges it.
        /// </summary>
        Task<BREBPayResult> PayBillAsync(
            int branchId, string smsAccountNo, string? username, CancellationToken cancellationToken = default);

        Task<BREBPaymentSubmitResponse?> SubmitPaymentAsync(
            int branchId, int transactionId, CancellationToken cancellationToken = default);

        Task<int> SaveBillingTransactionAsync(
            int branchId, BREBBillingInfoResponse billingInfo, string? username, CancellationToken cancellationToken = default);

        Task<bool> AcknowledgePaymentAsync(
            int branchId, int transactionId, string? username, CancellationToken cancellationToken = default);

        /// <summary>Asks BREB for the payment status and brings the local record in line. Returns a message for the user.</summary>
        Task<string> SyncPaymentStatusAsync(
            int branchId, int transactionId, string? username, CancellationToken cancellationToken = default);

        /// <summary>BREB Dispute API. disputeType: 5 = Acknowledge, 10 = Reset.</summary>
        Task<string> SubmitDisputeAsync(
            int branchId, int transactionId, int disputeType, string? username, CancellationToken cancellationToken = default);

        /// <summary>Collections created on one day (the screen defaults to today).</summary>
        Task<List<BREBTransaction>> GetCollectionListAsync(int branchId, DateTime date, CancellationToken cancellationToken = default);
        /// <summary>Unresolved payments (Failed / Pending / Waiting). date = null shows all days.</summary>
        Task<List<BREBTransaction>> GetUnsuccessfulListAsync(int branchId, DateTime? date, CancellationToken cancellationToken = default);
        Task<List<BREBTransaction>> GetDisputeListAsync(int branchId, DateTime? date, CancellationToken cancellationToken = default);

        /// <summary>Dispute form of the old project: Payer Transaction Id + SMS account + type (5 / 10).</summary>
        Task<string> SubmitDisputeAsync(
            int branchId, string payerTransactionId, string smsAccountNo, int disputeType, string? username,
            CancellationToken cancellationToken = default);

        /// <summary>Receipt data; null unless the payment is Success and belongs to the branch.</summary>
        Task<BREBReceiptViewModel?> GetReceiptAsync(int branchId, int transactionId, CancellationToken cancellationToken = default);

        /// <summary>Local transactions of one day compared with BREB /report/detail.</summary>
        Task<BREBReconcileReportViewModel> GetReconcileReportAsync(
            int branchId, DateTime date, CancellationToken cancellationToken = default);

        /// <summary>Local daily totals compared with BREB /report/summary.</summary>
        Task<BREBSummaryReportViewModel> GetSummaryReportAsync(
            int branchId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

        Task<BREBCollectionReportViewModel> GetCollectionReportAsync(
            int branchId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }
}
