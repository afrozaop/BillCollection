namespace BillCollection.Models.BREB
{
    public class BREBPaymentAcknowledgmentRequest
    {
        public string SMSAccountNo { get; set; } = string.Empty;

        public string RefNo { get; set; } = string.Empty;

        public string? PayerTransactionId { get; set; }
    }
}