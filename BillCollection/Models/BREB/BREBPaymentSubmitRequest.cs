namespace BillCollection.Models.BREB
{
    public class BREBPaymentSubmitRequest
    {
        public string SMSAccountNo { get; set; } = string.Empty;

        public string RefNo { get; set; } = string.Empty;

        public string PayerTransactionId { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }
}