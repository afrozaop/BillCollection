namespace BillCollection.Models.BREB
{
    public class BREBPaymentAcknowledgmentResponse
    {
        public int Status { get; set; }

        public BREBPaymentAcknowledgmentData? Data { get; set; }
    }

    public class BREBPaymentAcknowledgmentData
    {
        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        public string? PayerTransactionId { get; set; }

        public string? Message { get; set; }
    }
}