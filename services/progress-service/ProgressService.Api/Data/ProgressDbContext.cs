using Microsoft.EntityFrameworkCore;
using ProgressService.Api.Entities;

namespace ProgressService.Api.Data;

public sealed class ProgressDbContext(DbContextOptions<ProgressDbContext> options) : DbContext(options)
{
    public DbSet<StudyLog> StudyLogs => Set<StudyLog>();
    public DbSet<StudentProgress> StudentProgress => Set<StudentProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("progress");

        modelBuilder.Entity<StudyLog>(entity =>
        {
            entity.ToTable("study_logs");
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.UserId, s.StudyDate }).IsUnique();
        });

        modelBuilder.Entity<StudentProgress>(entity =>
        {
            entity.ToTable("student_progress");
            entity.HasKey(p => p.UserId);
            entity.Property(p => p.ScoreSum).HasColumnType("decimal(10,2)");
        });
    }
}
