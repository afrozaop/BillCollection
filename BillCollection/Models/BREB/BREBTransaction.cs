namespace BillCollection.Models.BREB
{
    public class BREBTransaction
    {
        public int Id { get; set; }

        // Branch information
        public int BranchId { get; set; }

        // BREB payment information
        public string PayerTransactionId { get; set; } = string.Empty;

        public string? RefNo { get; set; }

        public DateTime? PaymentTimestamp { get; set; }

        public string? SMSAccountNo { get; set; }

        public string? AccountName { get; set; }

        public string? DueType { get; set; }

        public string? BillMonth { get; set; }

        public decimal Amount { get; set; }

        public decimal Vat { get; set; }

        public string? Message { get; set; }

        public string? ContactNo { get; set; }

        public string? ExpiresIn { get; set; }

        // Transaction status
        public string? TxnStatus { get; set; }

        // Audit information
        public string? CreatedBy { get; set; }

        public DateTime CreatedOn { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedOn { get; set; }

        // Dispute information
        public string? DisputeType { get; set; }

        public string? DisputedBy { get; set; }

        public DateTime? DisputedOn { get; set; }
    }
}