using AuthService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Khoa> Khoas => Set<Khoa>();
    public DbSet<Lop> Lops => Set<Lop>();
    public DbSet<LopActivityLog> LopActivityLogs => Set<LopActivityLog>();
    public DbSet<MonHoc> MonHocs => Set<MonHoc>();
    public DbSet<MonHocLop> MonHocLops => Set<MonHocLop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).IsRequired().HasMaxLength(32);
            entity.Property(u => u.ChucVu).IsRequired().HasMaxLength(32).HasDefaultValue(ChucVuValues.HocVien);
            entity.Property(u => u.AvatarUrl).HasMaxLength(1024);
            entity.Property(u => u.AvatarPublicId).HasMaxLength(512);
            entity.HasOne<Lop>().WithMany().HasForeignKey(u => u.LopId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Khoa>(entity =>
        {
            entity.ToTable("khoa");
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Ten).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<Lop>(entity =>
        {
            entity.ToTable("lop");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Ten).IsRequired().HasMaxLength(128);
            entity.Property(l => l.NamHoc).HasMaxLength(32);
            // Restrict (không cascade) ở cả 2 quan hệ — tránh lỗi "multiple cascade paths" của SQL
            // Server (Lop tham chiếu cả Khoa lẫn User/GiaoVienId), và tránh xóa ngầm ẩn ý — xóa
            // Khóa/Giáo viên đang được Lớp tham chiếu phải xử lý tường minh ở tầng service.
            entity.HasOne<Khoa>().WithMany().HasForeignKey(l => l.KhoaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(l => l.GiaoVienId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LopActivityLog>(entity =>
        {
            entity.ToTable("lop_activity_logs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.ActionType).IsRequired().HasMaxLength(32);
            entity.Property(l => l.OldValue).HasMaxLength(256);
            entity.Property(l => l.NewValue).HasMaxLength(256);
            // Không HasOne/FK tới Lop/User — xem remarks trên entity, đây là nhật ký lịch sử.
            entity.HasIndex(l => new { l.LopId, l.CreatedAtUtc });
        });

        // Rà soát Lần XVI (2026-08-21) — Môn học (panel "Quản lý Môn học").
        modelBuilder.Entity<MonHoc>(entity =>
        {
            entity.ToTable("mon_hoc");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Ten).IsRequired().HasMaxLength(256);
            entity.Property(m => m.MaHocPhan).IsRequired().HasMaxLength(50);
            // Restrict, cùng lý do Lop.GiaoVienId ở trên — xóa GV đang đảm nhiệm 1 Môn học phải xử
            // lý tường minh ở tầng service (gỡ gán trước), không cascade ngầm.
            entity.HasOne<User>().WithMany().HasForeignKey(m => m.GiaoVienId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MonHocLop>(entity =>
        {
            entity.ToTable("mon_hoc_lop");
            entity.HasKey(ml => new { ml.MonHocId, ml.LopId });
            entity.HasOne<MonHoc>().WithMany().HasForeignKey(ml => ml.MonHocId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Lop>().WithMany().HasForeignKey(ml => ml.LopId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
