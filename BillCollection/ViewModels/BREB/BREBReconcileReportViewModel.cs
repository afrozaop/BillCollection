using BillCollection.Models.BREB;

namespace BillCollection.ViewModels.BREB
{
    public class BREBReconcileReportViewModel
    {
        public DateTime ReportDate { get; set; } = DateTime.Today;
        public bool IsMockMode { get; set; }

        /// <summary>Set when the BREB side could not be loaded (VPN down, auth error ...).</summary>
        public string? BrebError { get; set; }

        // Local figures for the day
        public int TotalTransactions { get; set; }
        public int SuccessfulPayments { get; set; }
        public int PendingPayments { get; set; }
        public int FailedPayments { get; set; }
        public decimal TotalSuccessfulAmount { get; set; }

        // BREB figures for the day
        public int BrebPaidCount { get; set; }
        public decimal BrebPaidAmount { get; set; }
        public int BrebWaitingCount { get; set; }

        public int MatchedCount { get; set; }
        public int MismatchCount { get; set; }

        public List<BREBReconcileRow> Rows { get; set; } = new();

        public List<BREBTransaction> Transactions { get; set; } = new();
    }

    public class BREBReconcileRow
    {
        public int? TransactionId { get; set; }
        public string? SMSAccountNo { get; set; }
        public string? RefNo { get; set; }
        public string? PayerTransactionId { get; set; }
        public decimal Amount { get; set; }
        public decimal Vat { get; set; }
        public string? LocalStatus { get; set; }
        public string? BrebStatus { get; set; }

        // Matched | Mismatch | Only at BREB
        public string Result { get; set; } = "Matched";
        public string? Note { get; set; }

        public bool CanSync { get; set; }
        public bool CanDispute { get; set; }
    }

    /// <summary>Model of the _RowActions partial (Check &amp; Sync + Dispute buttons).</summary>
    public class BREBRowActionViewModel
    {
        public int TransactionId { get; set; }
        public string ReturnTo { get; set; } = "ReconcileReport";
        public DateTime? Date { get; set; }
        public bool CanDispute { get; set; }
    }

    public class BREBSummaryReportViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public bool IsMockMode { get; set; }
        public string? BrebError { get; set; }
        public List<BREBSummaryRow> Rows { get; set; } = new();

        public int LocalCount => Rows.Sum(r => r.LocalCount);
        public decimal LocalAmount => Rows.Sum(r => r.LocalAmount);
        public int BrebCount => Rows.Sum(r => r.BrebCount);
        public decimal BrebAmount => Rows.Sum(r => r.BrebAmount);
    }

    public class BREBSummaryRow
    {
        public DateTime Date { get; set; }
        public int LocalCount { get; set; }
        public decimal LocalAmount { get; set; }
        public decimal LocalVat { get; set; }
        public int BrebCount { get; set; }
        public decimal BrebAmount { get; set; }
        public decimal BrebVat { get; set; }

        public bool Match => LocalCount == BrebCount && LocalAmount == BrebAmount;
    }

    public class BREBCollectionReportViewModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<BREBTransaction> Items { get; set; } = new();
        public int TotalCount => Items.Count;
        public decimal TotalAmount => Items.Sum(x => x.Amount);
        public decimal TotalVat => Items.Sum(x => x.Vat);
    }
}
