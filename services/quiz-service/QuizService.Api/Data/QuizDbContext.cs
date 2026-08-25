using Microsoft.EntityFrameworkCore;
using QuizService.Api.Entities;

namespace QuizService.Api.Data;

public sealed class QuizDbContext(DbContextOptions<QuizDbContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<EssayQuestion> EssayQuestions => Set<EssayQuestion>();
    public DbSet<OralQuestion> OralQuestions => Set<OralQuestion>();
    public DbSet<OralResult> OralResults => Set<OralResult>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<QuizResult> QuizResults => Set<QuizResult>();
    public DbSet<WrongAnswer> WrongAnswers => Set<WrongAnswer>();
    public DbSet<ExamSet> ExamSets => Set<ExamSet>();
    public DbSet<ExamVersion> ExamVersions => Set<ExamVersion>();
    public DbSet<ExamVersionQuestion> ExamVersionQuestions => Set<ExamVersionQuestion>();
    public DbSet<ExamVersionOralQuestion> ExamVersionOralQuestions => Set<ExamVersionOralQuestion>();
    public DbSet<QuestionLopVisibility> QuestionLopVisibilities => Set<QuestionLopVisibility>();
    public DbSet<EssayQuestionLopVisibility> EssayQuestionLopVisibilities => Set<EssayQuestionLopVisibility>();
    public DbSet<ExamVersionLopVisibility> ExamVersionLopVisibilities => Set<ExamVersionLopVisibility>();
    public DbSet<OralQuestionLopVisibility> OralQuestionLopVisibilities => Set<OralQuestionLopVisibility>();
    public DbSet<PracticeSet> PracticeSets => Set<PracticeSet>();
    public DbSet<PracticeSetLopVisibility> PracticeSetLopVisibilities => Set<PracticeSetLopVisibility>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("quiz");

        modelBuilder.Entity<Question>(entity =>
        {
            entity.ToTable("questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Chapter).HasMaxLength(128);
            entity.Property(q => q.QuestionText).HasMaxLength(2000);
            entity.Property(q => q.OptionA).HasMaxLength(500);
            entity.Property(q => q.OptionB).HasMaxLength(500);
            entity.Property(q => q.OptionC).HasMaxLength(500);
            entity.Property(q => q.OptionD).HasMaxLength(500);
            entity.Property(q => q.Explanation).HasMaxLength(2000);
            entity.Property(q => q.SourceType).HasMaxLength(32).HasDefaultValue("Manual");
            entity.Property(q => q.Topic).HasMaxLength(128);
            entity.Property(q => q.IsPublishedForPractice).HasDefaultValue(false);
            entity.HasIndex(q => q.Chapter);
            entity.HasIndex(q => q.Difficulty);
        });

        modelBuilder.Entity<EssayQuestion>(entity =>
        {
            entity.ToTable("essay_questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Chapter).HasMaxLength(128);
            entity.Property(q => q.QuestionText).HasMaxLength(2000);
            entity.Property(q => q.SuggestedAnswer).HasMaxLength(4000);
            entity.Property(q => q.SourceType).HasMaxLength(32).HasDefaultValue("Manual");
            entity.Property(q => q.IsPublishedForPractice).HasDefaultValue(false);
            entity.HasIndex(q => q.Chapter);
        });

        modelBuilder.Entity<OralQuestion>(entity =>
        {
            entity.ToTable("oral_questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Chapter).HasMaxLength(128);
            entity.Property(q => q.QuestionText).HasMaxLength(2000);
            entity.Property(q => q.ExpectedAnswer).HasMaxLength(4000);
            entity.HasIndex(q => q.Chapter);
        });

        modelBuilder.Entity<OralResult>(entity =>
        {
            entity.ToTable("oral_results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.MainAnswer).HasMaxLength(4000);
            entity.Property(r => r.AiScore).HasColumnType("decimal(4,2)");
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.ExamSessionId);
            // Restrict, not Cascade: an OralResult is a student's graded record (score, AI
            // comment, rubric — see F5 remediation). Deleting a question bank entry must not
            // silently wipe that history; OralQuestionService.DeleteAsync checks for this first
            // and returns a clean 409 instead of letting the DB throw an FK violation (Phần B
            // CRUD audit finding).
            entity.HasOne<OralQuestion>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamResult>(entity =>
        {
            entity.ToTable("exam_results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Score).HasColumnType("decimal(4,2)");
            entity.Property(r => r.IsAutoSubmitted).HasDefaultValue(false);
            entity.HasIndex(r => r.UserId);
        });

        // Việc 4.1 (2026-08-19) — xem ExamSession.cs remarks cho bất biến "1 InProgress/user/kind".
        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.ToTable("exam_sessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Kind).HasMaxLength(16);
            entity.Property(s => s.Status).HasMaxLength(32).HasDefaultValue(ExamSessionStatus.InProgress);
            entity.Property(s => s.RowVersion).IsRowVersion();
            entity.HasIndex(s => new { s.UserId, s.Kind, s.Status });
        });

        modelBuilder.Entity<QuizResult>(entity =>
        {
            entity.ToTable("quiz_results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Score).HasColumnType("decimal(4,2)");
            entity.Property(r => r.Chapter).HasMaxLength(128);
            entity.HasIndex(r => r.UserId);
        });

        modelBuilder.Entity<WrongAnswer>(entity =>
        {
            entity.ToTable("wrong_answers");
            entity.HasKey(w => new { w.UserId, w.QuestionId });
            entity.HasOne<Question>().WithMany().HasForeignKey(w => w.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamSet>(entity =>
        {
            entity.ToTable("exam_sets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ten).HasMaxLength(200);
        });

        modelBuilder.Entity<ExamVersion>(entity =>
        {
            entity.ToTable("exam_versions");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.MaDe).HasMaxLength(32);
            entity.Property(v => v.IsPublished).HasDefaultValue(false);
            entity.Property(v => v.Kind).HasMaxLength(16).HasDefaultValue("Mcq");
            entity.HasOne<ExamSet>().WithMany().HasForeignKey(v => v.ExamSetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamVersionQuestion>(entity =>
        {
            entity.ToTable("exam_version_questions");
            entity.HasKey(evq => new { evq.ExamVersionId, evq.QuestionId });
            entity.HasOne<ExamVersion>().WithMany().HasForeignKey(evq => evq.ExamVersionId).OnDelete(DeleteBehavior.Cascade);
            // Cascade ở tầng DB (khác OralResult Restrict) — nhưng QuestionService.DeleteAsync giờ
            // chặn ở tầng ứng dụng TRƯỚC khi xóa nếu Question thuộc 1 ExamVersion.IsPublished=true
            // (audit "Việc 1" xác nhận cascade từng gây co lại âm thầm mã đề đã xuất bản). Với mã đề
            // CHƯA publish, cascade vẫn cho xóa tự do — chấp nhận được, cùng nguyên tắc WrongAnswer.
            entity.HasOne<Question>().WithMany().HasForeignKey(evq => evq.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamVersionOralQuestion>(entity =>
        {
            entity.ToTable("exam_version_oral_questions");
            entity.HasKey(evq => new { evq.ExamVersionId, evq.OralQuestionId });
            entity.HasOne<ExamVersion>().WithMany().HasForeignKey(evq => evq.ExamVersionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<OralQuestion>().WithMany().HasForeignKey(evq => evq.OralQuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Việc 8 (2026-08-16) — 3 bảng nối "phạm vi hiển thị theo Lớp". LopId không có FK (soft-ref
        // sang auth-service.Lop, khác database schema) nhưng vẫn được index để lọc nhanh ở
        // ListForPracticeAsync. Không dòng nào cho 1 Question/EssayQuestion/ExamVersion = toàn hệ
        // thống (mặc định, tương thích ngược 100% với dữ liệu trước Việc 8).
        modelBuilder.Entity<QuestionLopVisibility>(entity =>
        {
            entity.ToTable("question_lop_visibility");
            entity.HasKey(v => new { v.QuestionId, v.LopId });
            entity.HasIndex(v => v.LopId);
            entity.HasOne<Question>().WithMany().HasForeignKey(v => v.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EssayQuestionLopVisibility>(entity =>
        {
            entity.ToTable("essay_question_lop_visibility");
            entity.HasKey(v => new { v.EssayQuestionId, v.LopId });
            entity.HasIndex(v => v.LopId);
            entity.HasOne<EssayQuestion>().WithMany().HasForeignKey(v => v.EssayQuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExamVersionLopVisibility>(entity =>
        {
            entity.ToTable("exam_version_lop_visibility");
            entity.HasKey(v => new { v.ExamVersionId, v.LopId });
            entity.HasIndex(v => v.LopId);
            entity.HasOne<ExamVersion>().WithMany().HasForeignKey(v => v.ExamVersionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Việc 4.4 Phần A (2026-08-20) — bảng thứ 4, vá gap câu hỏi Vấn đáp chưa có visibility.
        modelBuilder.Entity<OralQuestionLopVisibility>(entity =>
        {
            entity.ToTable("oral_question_lop_visibility");
            entity.HasKey(v => new { v.OralQuestionId, v.LopId });
            entity.HasIndex(v => v.LopId);
            entity.HasOne<OralQuestion>().WithMany().HasForeignKey(v => v.OralQuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Việc 4.4 Phần B (2026-08-20) — "Đề luyện tập" giáo viên tạo, xem remarks PracticeSet.cs.
        modelBuilder.Entity<PracticeSet>(entity =>
        {
            entity.ToTable("practice_sets");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Ten).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Chapter).IsRequired().HasMaxLength(128);
            entity.HasIndex(p => p.GiaoVienId);
        });

        modelBuilder.Entity<PracticeSetLopVisibility>(entity =>
        {
            entity.ToTable("practice_set_lop_visibility");
            entity.HasKey(v => new { v.PracticeSetId, v.LopId });
            entity.HasIndex(v => v.LopId);
            entity.HasOne<PracticeSet>().WithMany().HasForeignKey(v => v.PracticeSetId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
