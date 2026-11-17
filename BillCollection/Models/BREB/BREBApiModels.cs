using System.Text.Json.Serialization;

namespace BillCollection.Models.BREB
{
    // ICBS standard envelope: { "Status": 200, "Data": {...}, "Errors": [ {Code, Message} ] }
    public class BREBApiResponse<T>
    {
        public int Status { get; set; }
        public T? Data { get; set; }
        public List<BREBApiError> Errors { get; set; } = new();

        [JsonIgnore]
        public bool IsSuccess => Status == 200 && Errors.Count == 0;

        [JsonIgnore]
        public string ErrorText =>
            Errors.Count == 0
                ? $"BREB returned status {Status}."
                : string.Join(" | ", Errors.Select(e => $"{e.Code} - {e.Message}"));

        public bool HasError(string code) =>
            Errors.Any(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public class BREBApiError
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))]
        public string? Code { get; set; }
        public string? Message { get; set; }
    }

    public class BREBApiException : Exception
    {
        public int Status { get; }
        public List<BREBApiError> Errors { get; }

        /// <summary>True when we do NOT know whether BREB processed the request (timeout / network).</summary>
        public bool OutcomeUnknown { get; }

        public BREBApiException(string message, int status = 0,
            List<BREBApiError>? errors = null, bool outcomeUnknown = false, Exception? inner = null)
            : base(message, inner)
        {
            Status = status;
            Errors = errors ?? new();
            OutcomeUnknown = outcomeUnknown;
        }

        public bool HasError(string code) =>
            Errors.Any(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    // ----- Payment status (7.1) -----
    public class BREBPaymentStatusRequest
    {
        public string SMSAccountNo { get; set; } = string.Empty;
        public string RefNo { get; set; } = string.Empty;
        public string? PayerTransactionId { get; set; }
    }

    public class BREBPaymentStatusData
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? SMSAccountNo { get; set; }
        public string? RefNo { get; set; }
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? PayerTransactionId { get; set; }
        // 8 Reset | 9 Submitted for Dispute | 10 Pending | 30 Waiting for Acknowledge | 50 Paid Acknowledged
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? Status { get; set; }
    }

    // ----- Dispute (8): 5 = Acknowledge, 10 = Reset -----
    public class BREBDisputeRequest
    {
        public string SMSAccountNo { get; set; } = string.Empty;
        public string RefNo { get; set; } = string.Empty;
        public string PayerTransactionId { get; set; } = string.Empty;
        public int DisputeType { get; set; }
    }

    public class BREBDisputeData
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? SMSAccountNo { get; set; }
        public string? RefNo { get; set; }
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? PayerTransactionId { get; set; }
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? DisputeType { get; set; }
        public string? Message { get; set; }
    }

    // ----- Reports (7.2 / 7.3) -----
    public class BREBReportDetailRequest
    {
        public string CollectionDate { get; set; } = string.Empty;   // yyyy-MM-dd
        public int? CollectionStatus { get; set; }                   // 30 | 50 (default 50)
        public string? BranchRoutingNo { get; set; }
        public string? AgentId { get; set; }
    }

    public class BREBReportDetailData
    {
        public int TotalCount { get; set; }
        public List<BREBReportDetailEntity> Entities { get; set; } = new();
    }

    public class BREBReportDetailEntity
    {
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? SMSAccountNo { get; set; }
        public string? RefNo { get; set; }
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? PayerTransactionId { get; set; }
        [JsonConverter(typeof(BREBFlexibleStringConverter))] public string? BillType { get; set; }
        public decimal Amount { get; set; }
        public decimal Vat { get; set; }
        public string? Message { get; set; }
    }

    public class BREBReportSummaryRequest
    {
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? BranchRoutingNo { get; set; }
        public string? AgentId { get; set; }
    }

    public class BREBReportSummaryData
    {
        public int TotalCount { get; set; }
        public List<BREBReportSummaryEntity> Entities { get; set; } = new();
    }

    public class BREBReportSummaryEntity
    {
        public string? PaymentDate { get; set; }
        public int NoOfBill { get; set; }
        public decimal Amount { get; set; }
        public decimal Vat { get; set; }
    }

    // Result of the one-click "Pay Bill" flow
    public class BREBPayResult
    {
        public int TransactionId { get; set; }
        public string PayerTransactionId { get; set; } = string.Empty;
        public bool Submitted { get; set; }
        public bool Acknowledged { get; set; }

        /// <summary>Submitted OK, acknowledgement is left to the teller (AutoAcknowledge = false).</summary>
        public bool AwaitingAcknowledge { get; set; }
        public string? Message { get; set; }
    }
}
