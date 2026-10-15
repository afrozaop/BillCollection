using BillCollection.Models.BREB;

namespace BillCollection.ViewModels.BREB
{
    public class BREBReconcileReportViewModel
    {
        public int TotalTransactions { get; set; }
        public int SuccessfulPayments { get; set; }
        public int PendingPayments { get; set; }
        public int FailedPayments { get; set; }
        public decimal TotalSuccessfulAmount { get; set; }

        public List<BREBTransaction> Transactions { get; set; }
            = new List<BREBTransaction>();
    }
}