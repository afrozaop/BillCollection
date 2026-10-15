namespace BillCollection.Models.BREB
{
    public class BREBPaymentSubmitResponse
    {
        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        public string? PayerTransactionId { get; set; }

        public string? ExpiresAt { get; set; }

        public string? Message { get; set; }

        public bool Success { get; set; }
    }
}