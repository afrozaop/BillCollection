using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BillCollection.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<BillProvider> BillProviders { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchBillProvider> BranchBillProviders { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=(local);Database=BillCollectionDB;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.BranchRoutingNumber).HasMaxLength(9);
            entity.Property(e => e.CanEditCbsDetails).HasDefaultValue(true);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Designation).HasMaxLength(80);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasMaxLength(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MobileNumber).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Branch).WithMany(p => p.AppUsers)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK_AppUsers_Branches");
        });

        modelBuilder.Entity<BillProvider>(entity =>
        {
            entity.HasIndex(e => e.ProviderCode, "UQ_BillProviders_ProviderCode").IsUnique();

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ProviderCode).HasMaxLength(30);
            entity.Property(e => e.ProviderName).HasMaxLength(100);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.BranchCode).HasMaxLength(20);
            entity.Property(e => e.ContactNumber).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.District).HasMaxLength(80);
            entity.Property(e => e.Email).HasMaxLength(120);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ManagerName).HasMaxLength(120);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.NameBangla).HasMaxLength(150);
            entity.Property(e => e.PoliceStation).HasMaxLength(80);
            entity.Property(e => e.RoutingNumber).HasMaxLength(9);
        });

        modelBuilder.Entity<BranchBillProvider>(entity =>
        {
            entity.ToTable("BranchBillProviders", "dbo");

            entity.HasIndex(e => new { e.BranchId, e.ProviderId }, "UQ_BranchBillProviders").IsUnique();

            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.HasOne(d => d.Branch)
                .WithMany(p => p.BranchBillProviders)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BranchBillProviders_Branches");

            entity.HasOne(d => d.Provider)
                .WithMany(p => p.BranchBillProviders)
                .HasForeignKey(d => d.ProviderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BranchBillProviders_BillProviders");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
