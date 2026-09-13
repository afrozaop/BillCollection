using Microsoft.EntityFrameworkCore;

namespace BillCollection.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<BillProvider> BillProviders { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchBillProvider>
        BranchBillProviders
    { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // =====================================================
        // APP USER
        // =====================================================

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.BranchRoutingNumber)
                .HasMaxLength(9);

            entity.Property(e => e.CanEditCbsDetails)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(50);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql(
                    "(sysutcdatetime())");

            entity.Property(e => e.Designation)
                .HasMaxLength(80);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .HasMaxLength(100);

            entity.Property(e => e.EmployeeId)
                .HasMaxLength(30);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.MobileNumber)
                .HasMaxLength(20);

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(200);

            entity.Property(e => e.Role)
                .HasMaxLength(20);

            entity.Property(e => e.Username)
                .HasMaxLength(50);

            entity.HasOne(e => e.Branch)
                .WithMany(e => e.AppUsers)
                .HasForeignKey(e => e.BranchId)
                .HasConstraintName(
                    "FK_AppUsers_Branches");
        });

        // =====================================================
        // BILL PROVIDER
        // =====================================================

        modelBuilder.Entity<BillProvider>(entity =>
        {
            entity.HasIndex(
                e => e.ProviderCode,
                "UQ_BillProviders_ProviderCode")
                .IsUnique();

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql(
                    "(sysutcdatetime())");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.ProviderCode)
                .HasMaxLength(30);

            entity.Property(e => e.ProviderName)
                .HasMaxLength(100);
        });

        // =====================================================
        // BRANCH
        // =====================================================

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(e => e.Address)
                .HasMaxLength(250);

            entity.Property(e => e.BranchCode)
                .HasMaxLength(20);

            entity.Property(e => e.ContactNumber)
                .HasMaxLength(50);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql(
                    "(sysutcdatetime())");

            entity.Property(e => e.District)
                .HasMaxLength(80);

            entity.Property(e => e.Email)
                .HasMaxLength(120);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.ManagerName)
                .HasMaxLength(120);

            entity.Property(e => e.Name)
                .HasMaxLength(150);

            entity.Property(e => e.NameBangla)
                .HasMaxLength(150);

            entity.Property(e => e.PoliceStation)
                .HasMaxLength(80);

            entity.Property(e => e.RoutingNumber)
                .HasMaxLength(9);
        });

        // =====================================================
        // BRANCH BILL PROVIDER
        // =====================================================

        modelBuilder.Entity<BranchBillProvider>(entity =>
        {
            entity.ToTable(
                "BranchBillProviders",
                "dbo");

            entity.HasIndex(
                e => new
                {
                    e.BranchId,
                    e.ProviderId
                },
                "UQ_BranchBillProviders")
                .IsUnique();

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(50);

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql(
                    "(sysutcdatetime())");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.HasOne(e => e.Branch)
                .WithMany(e => e.BranchBillProviders)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(
                    DeleteBehavior.ClientSetNull)
                .HasConstraintName(
                    "FK_BranchBillProviders_Branches");

            entity.HasOne(e => e.Provider)
                .WithMany(e => e.BranchBillProviders)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(
                    DeleteBehavior.ClientSetNull)
                .HasConstraintName(
                    "FK_BranchBillProviders_BillProviders");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder);
}