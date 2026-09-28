using BillCollection.Interfaces;
using BillCollection.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Services
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<AppUser> _passwordHasher;

        public AdminService(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<AppUser>();
        }

        // =========================================================
        // CREATE USER
        // =========================================================

        public async Task<bool> UsernameExistsAsync(string username)
        {
            username = username.Trim();

            return await _context.AppUsers
                .AnyAsync(x => x.Username == username);
        }

        public async Task CreateUserAsync(
            AppUser user,
            string password,
            string createdBy)
        {
            user.Username = user.Username.Trim();

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    password);

            user.CreatedDate = DateTime.Now;
            user.CreatedBy = createdBy;

            _context.AppUsers.Add(user);

            await _context.SaveChangesAsync();
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
        // SEARCH + PAGINATION
        // =========================================================

        public async Task<(List<AppUser> Users, int TotalRecords)>
            GetUsersAsync(
                string? search,
                int page,
                int pageSize)
        {
            // Safety
            if (page < 1)
                page = 1;

            if (pageSize <= 0)
                pageSize = 25;

            if (pageSize > 100)
                pageSize = 100;


            IQueryable<AppUser> query =
                _context.AppUsers
                    .AsNoTracking();


            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(x =>
                    x.Username.Contains(search) ||
                    x.DisplayName.Contains(search) ||
                    x.Role.Contains(search));
            }


            // Total records BEFORE pagination
            int totalRecords =
                await query.CountAsync();


            // Pagination
            List<AppUser> users =
                await query
                    .OrderBy(x => x.Username)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();


            return (users, totalRecords);
        }


        // =========================================================
        // GET ALL USERS
        // =========================================================

        public async Task<List<AppUser>> GetUsersAsync()
        {
            return await _context.AppUsers
                .AsNoTracking()
                .OrderBy(x => x.Username)
                .ToListAsync();
        }


        // =========================================================
        // GET SINGLE USER
        // =========================================================

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

        public async Task<bool>
            CreateBillProviderAsync(
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

        public async Task<bool>
            UpdateBillProviderAsync(
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

        public async Task
            DeactivateBillProviderAsync(int id)
        {
            var provider =
                await _context.BillProviders
                    .FirstOrDefaultAsync(
                        x => x.Id == id);

            if (provider == null)
                return;


            provider.IsActive = false;

            await _context.SaveChangesAsync();
        }


        // =========================================================
        // ACTIVATE BILL PROVIDER
        // =========================================================

        public async Task
            ActivateBillProviderAsync(int id)
        {
            var provider =
                await _context.BillProviders
                    .FirstOrDefaultAsync(
                        x => x.Id == id);

            if (provider == null)
                return;


            provider.IsActive = true;

            await _context.SaveChangesAsync();
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


        // =========================================================
        // DELETE BILL PROVIDER
        // =========================================================
        // NOTE:
        // Hard delete can cause FK error if provider is assigned
        // to BranchBillProviders.
        // =========================================================

        public async Task
            DeleteBillProviderAsync(int id)
        {
            var provider =
                await _context.BillProviders
                    .FirstOrDefaultAsync(
                        p => p.Id == id);

            if (provider == null)
                return;


            _context.BillProviders
                .Remove(provider);

            await _context.SaveChangesAsync();
        }
        // =========================================================
        // RESET USER PASSWORD
        // =========================================================

        public async Task ResetUserPasswordAsync(
            long id,
            string newPassword)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
            {
                throw new KeyNotFoundException(
                    "User not found.");
            }

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    newPassword);

            await _context.SaveChangesAsync();
        }
    }
}