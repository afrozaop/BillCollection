using BillCollection.Interfaces;
using BillCollection.Models;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Services
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;

        public AdminService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DASHBOARD
        // =========================================================

        public async Task<int> GetBranchCountAsync()
        {
            return await _context.Branches
                .CountAsync();
        }

        public async Task<int> GetUserCountAsync()
        {
            return await _context.AppUsers
                .CountAsync();
        }

        public async Task<int> GetProviderCountAsync()
        {
            return await _context.BillProviders
                .CountAsync();
        }

        public async Task<int> GetAssignmentCountAsync()
        {
            return await _context.BranchBillProviders
                .CountAsync();
        }


        // =========================================================
        // USERS
        // =========================================================

        public async Task<List<AppUser>> GetUsersAsync()
        {
            return await _context.AppUsers
                .AsNoTracking()
                .OrderBy(x => x.Username)
                .ToListAsync();
        }
        public async Task<AppUser?> GetUserAsync(long id)
        {
            return await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        // =========================================================
        // BILL PROVIDERS
        // =========================================================

        public async Task<List<BillProvider>>
            GetBillProvidersAsync()
        {
            return await _context.BillProviders
                .AsNoTracking()
                .OrderBy(x => x.ProviderName)
                .ToListAsync();
        }


        // =========================================================
        // GET SINGLE BILL PROVIDER
        // =========================================================

        public async Task<BillProvider?>
            GetBillProviderAsync(int id)
        {
            return await _context.BillProviders
                .FindAsync(id);
        }


        // =========================================================
        // CREATE BILL PROVIDER
        // =========================================================

        public async Task<bool> CreateBillProviderAsync(
            BillProvider provider)
        {
            provider.CreatedDate =
                DateTime.UtcNow;

            provider.IsActive = true;

            _context.BillProviders.Add(provider);

            await _context.SaveChangesAsync();

            return true;
        }


        // =========================================================
        // UPDATE BILL PROVIDER
        // =========================================================

        public async Task<bool> UpdateBillProviderAsync(
            int id,
            BillProvider provider)
        {
            var existing =
                await _context.BillProviders
                    .FindAsync(id);

            if (existing == null)
                return false;

            existing.ProviderCode =
                provider.ProviderCode;

            existing.ProviderName =
                provider.ProviderName;

            existing.IsActive =
                provider.IsActive;

            await _context.SaveChangesAsync();

            return true;
        }


        // =========================================================
        // DEACTIVATE BILL PROVIDER
        // =========================================================

        public async Task<bool>
            DeactivateBillProviderAsync(int id)
        {
            var provider =
                await _context.BillProviders
                    .FindAsync(id);

            if (provider == null)
                return false;

            provider.IsActive = false;

            await _context.SaveChangesAsync();

            return true;
        }


        // =========================================================
        // ACTIVE BRANCHES
        // =========================================================

        public async Task<List<Branch>>
            GetActiveBranchesAsync()
        {
            return await _context.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.BranchCode)
                .ToListAsync();
        }


        // =========================================================
        // ACTIVE BILL PROVIDERS
        // =========================================================

        public async Task<List<BillProvider>>
            GetActiveProvidersAsync()
        {
            return await _context.BillProviders
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.ProviderName)
                .ToListAsync();
        }


        // =========================================================
        // SELECTED PROVIDER IDS
        // =========================================================

        public async Task<List<int>>
            GetSelectedProviderIdsAsync(
                int branchId)
        {
            return await _context
                .BranchBillProviders
                .AsNoTracking()
                .Where(x =>
                    x.BranchId == branchId &&
                    x.IsActive)
                .Select(x => x.ProviderId)
                .Distinct()
                .ToListAsync();
        }


        // =========================================================
        // SAVE BRANCH PROVIDER ACCESS
        // =========================================================

        public async Task<bool>
            SaveBranchProviderAccessAsync(
                int branchId,
                int[] providerIds,
                string? createdBy)
        {
            // -----------------------------------------------------
            // CHECK BRANCH
            // -----------------------------------------------------

            var branch =
                await _context.Branches
                    .FirstOrDefaultAsync(x =>
                        x.Id == branchId &&
                        x.IsActive);

            if (branch == null)
                return false;


            // -----------------------------------------------------
            // SELECTED PROVIDERS
            // -----------------------------------------------------

            var selectedIds =
                providerIds?
                    .Distinct()
                    .ToList()
                ?? new List<int>();


            // -----------------------------------------------------
            // EXISTING ASSIGNMENTS
            // -----------------------------------------------------

            var existing =
                await _context.BranchBillProviders
                    .Where(x =>
                        x.BranchId == branchId)
                    .ToListAsync();


            // -----------------------------------------------------
            // REMOVE OLD ASSIGNMENTS
            // -----------------------------------------------------

            if (existing.Any())
            {
                _context.BranchBillProviders
                    .RemoveRange(existing);
            }


            // -----------------------------------------------------
            // ADD NEW ASSIGNMENTS
            // -----------------------------------------------------

            if (selectedIds.Any())
            {
                var validProviderIds =
                    await _context.BillProviders
                        .Where(x =>
                            x.IsActive &&
                            selectedIds.Contains(x.Id))
                        .Select(x => x.Id)
                        .Distinct()
                        .ToListAsync();

                foreach (var providerId
                         in validProviderIds)
                {
                    _context.BranchBillProviders.Add(
                        new BranchBillProvider
                        {
                            BranchId = branchId,
                            ProviderId = providerId,
                            IsActive = true,
                            CreatedDate =
                                DateTime.UtcNow,
                            CreatedBy = createdBy
                        });
                }
            }


            // -----------------------------------------------------
            // SAVE
            // -----------------------------------------------------

            await _context.SaveChangesAsync();

            return true;
        }
    }
}