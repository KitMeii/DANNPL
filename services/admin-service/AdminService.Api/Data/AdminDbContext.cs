using AdminService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Api.Data;

public sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<RoleChangeAudit> RoleChangeAudits => Set<RoleChangeAudit>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<LopDeletionAudit> LopDeletionAudits => Set<LopDeletionAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("admin");

        modelBuilder.Entity<RoleChangeAudit>(entity =>
        {
            entity.ToTable("role_change_audits");
            entity.HasKey(a => a.Id);
            // 32 to match User.Role in auth-service — both hold the same Roles.All values.
            entity.Property(a => a.OldRole).HasMaxLength(32);
            entity.Property(a => a.NewRole).HasMaxLength(32);
            entity.HasIndex(a => a.ChangedAtUtc);
        });

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("system_configs");
            entity.HasKey(c => c.Key);
            entity.Property(c => c.Key).HasMaxLength(128);
            // Matches SetConfigRequestValidator's MaximumLength(4000).
            entity.Property(c => c.Value).HasMaxLength(4000);
        });

        modelBuilder.Entity<LopDeletionAudit>(entity =>
        {
            entity.ToTable("lop_deletion_audits");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.LopTen).HasMaxLength(128);
            entity.Property(a => a.Status).HasMaxLength(32);
            entity.HasIndex(a => a.PreparationId).IsUnique();
            entity.HasIndex(a => a.LopId);
        });
    }
}
