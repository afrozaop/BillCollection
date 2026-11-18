namespace BillCollection.ViewModels.BREB
{
    /// <summary>Head-office "Postpaid Collection Summary": per branch, per day (successful payments only).</summary>
    public class BREBAdminSummaryReportViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string? BranchCode { get; set; }

        public List<BREBAdminSummaryBranchViewModel> Branches { get; set; } = new();

        public int GrandTxnCount => Branches.Sum(b => b.TxnCount);
        public decimal GrandRevenue => Branches.Sum(b => b.Revenue);
        public decimal GrandVat => Branches.Sum(b => b.Vat);
        public decimal GrandAmount => Branches.Sum(b => b.Amount);
    }

    public class BREBAdminSummaryBranchViewModel
    {
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public List<BREBAdminSummaryDayViewModel> Days { get; set; } = new();

        public int TxnCount => Days.Sum(d => d.TxnCount);
        public decimal Revenue => Days.Sum(d => d.Revenue);
        public decimal Vat => Days.Sum(d => d.Vat);
        public decimal Amount => Days.Sum(d => d.Amount);
    }

    public class BREBAdminSummaryDayViewModel
    {
        public DateTime Date { get; set; }
        public int TxnCount { get; set; }

        /// <summary>Amount (what the customer paid) minus VAT.</summary>
        public decimal Revenue => Amount - Vat;
        public decimal Vat { get; set; }

        /// <summary>Txn Amount = Revenue + VAT = the Amount the branch collected.</summary>
        public decimal Amount { get; set; }
    }
}
