using BillCollection.Models.BREB;

namespace BillCollection.Interfaces
{
    /// <summary>Thin, 1-to-1 wrapper of the ICBS/BREB payment API (spec v1.5).</summary>
    public interface IBREBApiClient
    {
        Task<BREBApiResponse<BREBBillingInfoResponse>> BillingInfoAsync(BREBBillingInfoRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBPaymentSubmitResponse>> PaymentSubmitAsync(BREBPaymentSubmitRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBPaymentAcknowledgmentData>> PaymentAcknowledgeAsync(BREBPaymentAcknowledgmentRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBPaymentStatusData>> PaymentStatusAsync(BREBPaymentStatusRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBDisputeData>> DisputeAsync(BREBDisputeRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBReportDetailData>> ReportDetailAsync(BREBReportDetailRequest request, CancellationToken ct = default);
        Task<BREBApiResponse<BREBReportSummaryData>> ReportSummaryAsync(BREBReportSummaryRequest request, CancellationToken ct = default);
    }
}
