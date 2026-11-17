
namespace BillCollection.ViewModels.BREB
{
    public class BREBAdminCollectionReportViewModel
    {
        // Search filters
        public DateTime CollectionDate { get; set; } = DateTime.Today;

        public string? BranchCode { get; set; }

        // Summary
        public int TotalTransactions { get; set; }

        public decimal TotalCollectionAmount { get; set; }

        public decimal TotalVat { get; set; }

        // Report rows
        public List<BREBAdminCollectionReportRowViewModel> Transactions
        {
            get; set;
        } = new();
    }

    public class BREBAdminCollectionReportRowViewModel
    {
        public DateTime? CollectionDate { get; set; }

        public string BranchCode { get; set; } = string.Empty;

        public string BranchName { get; set; } = string.Empty;

        public string? SMSAccountNo { get; set; }

        public string? RefNo { get; set; }

        public string? PayerTransactionId { get; set; }

        public decimal Amount { get; set; }

        public decimal Vat { get; set; }

        public string? TxnStatus { get; set; }
    }
}
