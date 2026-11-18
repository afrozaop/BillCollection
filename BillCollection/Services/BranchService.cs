using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Services
{
    public class BranchService : IBranchService
    {
        private readonly ApplicationDbContext _context;

        public BranchService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Branch?> GetBranchAsync(
            int branchId)
        {
            return await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == branchId &&
                    x.IsActive);
        }

        public async Task<List<BranchBillProvider>>
            GetAssignedProvidersAsync(
                int branchId)
        {
            return await _context
                .BranchBillProviders
                .AsNoTracking()
                .Include(x => x.Provider)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.IsActive &&
                    x.Provider.IsActive)
                .OrderBy(x =>
                    x.Provider.ProviderName)
                .ToListAsync();
        }

        // =========================================================
        // DASHBOARD: provider-wise summary
        // =========================================================

        public async Task<BranchDashboardViewModel?>
            GetDashboardAsync(
                int branchId)
        {
            var branch = await GetBranchAsync(branchId);

            if (branch == null)
            {
                return null;
            }

            var assigned = await GetAssignedProvidersAsync(branchId);
            var today = DateTime.Today;

            var model = new BranchDashboardViewModel
            {
                Branch = branch,
                Today = today
            };

            foreach (var item in assigned)
            {
                var code = item.Provider?.ProviderCode ?? string.Empty;

                // Only BREB has collection data in this application so far.
                // Add the other providers here when their transactions exist.
                var summary =
                    code == "BREB"
                        ? await BuildBrebSummaryAsync(branchId, today)
                        : new ProviderSummaryViewModel();

                summary.ProviderCode = code;
                summary.ProviderName = item.Provider?.ProviderName ?? code;

                model.Providers.Add(summary);
            }

            return model;
        }

        private async Task<ProviderSummaryViewModel>
            BuildBrebSummaryAsync(
                int branchId,
                DateTime today)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-6);
            var since = monthStart < weekStart ? monthStart : weekStart;
            var tomorrow = today.AddDays(1);

            // successful collections since the start of the month / last 7 days (a few hundred rows at most)
            var rows = await _context.BREBTransactions
                .AsNoTracking()
                .Where(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus == "Success" &&
                    x.CreatedOn >= since)
                .Select(x => new
                {
                    x.CreatedOn,
                    x.Amount,
                    x.Vat
                })
                .ToListAsync();

            var todayRows = rows.Where(x => x.CreatedOn >= today && x.CreatedOn < tomorrow).ToList();
            var monthRows = rows.Where(x => x.CreatedOn >= monthStart).ToList();

            var pending = await _context.BREBTransactions
                .AsNoTracking()
                .CountAsync(x =>
                    x.BranchId == branchId &&
                    (x.TxnStatus == "WaitingForAcknowledgement" || x.TxnStatus == "Pending"));

            var disputes = await _context.BREBTransactions
                .AsNoTracking()
                .CountAsync(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus == "Dispute");

            var failedToday = await _context.BREBTransactions
                .AsNoTracking()
                .CountAsync(x =>
                    x.BranchId == branchId &&
                    x.TxnStatus == "Failed" &&
                    x.CreatedOn >= today &&
                    x.CreatedOn < tomorrow);

            var summary = new ProviderSummaryViewModel
            {
                HasSummary = true,
                TodayCount = todayRows.Count,
                TodayAmount = todayRows.Sum(x => x.Amount),
                TodayVat = todayRows.Sum(x => x.Vat),
                MonthCount = monthRows.Count,
                MonthAmount = monthRows.Sum(x => x.Amount),
                LastCollectionOn = rows.Count == 0 ? null : rows.Max(x => x.CreatedOn),
                PendingCount = pending,
                DisputeCount = disputes,
                FailedTodayCount = failedToday
            };

            for (var i = 0; i < 7; i++)
            {
                var day = today.AddDays(-i);
                var next = day.AddDays(1);
                var dayRows = rows.Where(x => x.CreatedOn >= day && x.CreatedOn < next).ToList();

                summary.Last7Days.Add(new ProviderDaySummary
                {
                    Date = day,
                    Count = dayRows.Count,
                    Amount = dayRows.Sum(x => x.Amount)
                });
            }

            return summary;
        }

    }
}