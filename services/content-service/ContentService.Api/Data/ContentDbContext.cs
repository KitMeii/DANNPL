using ContentService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Api.Data;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<Material> Materials => Set<Material>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("content");

        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("materials");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Title).IsRequired().HasMaxLength(512);
            entity.Property(m => m.Chapter).HasMaxLength(128);
            entity.Property(m => m.FileName).IsRequired().HasMaxLength(512);
            entity.Property(m => m.FileUrl).IsRequired();
            entity.Property(m => m.CloudinaryPublicId).HasMaxLength(512);
            entity.HasIndex(m => m.Chapter);
        });
    }
}
