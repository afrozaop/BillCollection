using BillCollection.Models;

namespace BillCollection.ViewModels
{
    /// <summary>Branch dashboard: one summary block per bill provider assigned to the branch.</summary>
    public class BranchDashboardViewModel
    {
        public Branch Branch { get; set; } = null!;
        public DateTime Today { get; set; }
        public List<ProviderSummaryViewModel> Providers { get; set; } = new();

        // totals over the providers that have a summary
        public int TodayCount => Providers.Where(p => p.HasSummary).Sum(p => p.TodayCount);
        public decimal TodayAmount => Providers.Where(p => p.HasSummary).Sum(p => p.TodayAmount);
        public int MonthCount => Providers.Where(p => p.HasSummary).Sum(p => p.MonthCount);
        public decimal MonthAmount => Providers.Where(p => p.HasSummary).Sum(p => p.MonthAmount);
        public int AttentionCount => Providers.Where(p => p.HasSummary).Sum(p => p.AttentionCount);
    }

    public class ProviderSummaryViewModel
    {
        public string ProviderCode { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>false = no collection data is available for this provider (yet).</summary>
        public bool HasSummary { get; set; }

        // successful collections (Amount includes VAT)
        public int TodayCount { get; set; }
        public decimal TodayAmount { get; set; }
        public decimal TodayVat { get; set; }
        public int MonthCount { get; set; }
        public decimal MonthAmount { get; set; }
        public DateTime? LastCollectionOn { get; set; }

        // needs attention
        public int PendingCount { get; set; }      // waiting for acknowledgement / pending
        public int DisputeCount { get; set; }      // dispute sent, BREB decision awaited
        public int FailedTodayCount { get; set; }

        public int AttentionCount => PendingCount + DisputeCount;

        /// <summary>Newest day first.</summary>
        public List<ProviderDaySummary> Last7Days { get; set; } = new();
    }

    public class ProviderDaySummary
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
}
