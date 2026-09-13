using BillCollection.Interfaces;
using BillCollection.Models;
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
    }
}