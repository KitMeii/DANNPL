using System.Text.Json;
using AdminService.Api.Clients;
using AdminService.Api.Data;
using AdminService.Api.Dtos;
using AdminService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Services;

public sealed class LopDeletionService(
    AdminDbContext db,
    IAuthAdminClient authClient,
    IQuizLopDataClient quizClient,
    IProgressLopDataClient progressClient,
    ILogger<LopDeletionService> logger) : ILopDeletionService
{
    private static readonly TimeSpan PreparationValidity = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions BackupJsonOptions = new() { WriteIndented = true };

    public async Task<PrepareLopDeletionResponse> PrepareAsync(Guid lopId, Guid adminUserId, CancellationToken ct)
    {
        // CHỈ ĐỌC ở toàn bộ hàm này — không xóa gì. Nếu BẤT KỲ bước nào bên dưới ném lỗi, hàm kết
        // thúc bằng exception, KHÔNG có LopDeletionAudit nào được ghi, KHÔNG có PreparationId nào
        // phát sinh — đúng yêu cầu "backup thất bại thì không được xóa gì" (bổ sung an toàn #1).
        var lop = await authClient.GetLopAsync(lopId, ct);
        var students = await authClient.ListHocVienAsync(lopId, ct);
        var userIds = students.Select(s => s.Id).ToList();

        var quizDump = await quizClient.DumpAsync(userIds, lopId, ct);
        var progressDump = await progressClient.DumpAsync(userIds, ct);
        var activityLogs = await authClient.ListAllLopActivityLogAsync(lopId, ct);

        var counts = new LopDeletionCounts(
            StudentsCount: students.Count,
            QuizResultsCount: quizDump.QuizResults.Count,
            ExamResultsCount: quizDump.ExamResults.Count,
            ExamSessionsCount: quizDump.ExamSessions.Count,
            OralResultsCount: quizDump.OralResults.Count,
            WrongAnswersCount: quizDump.WrongAnswers.Count,
            QuestionVisibilityCount: quizDump.QuestionVisibilityIds.Count,
            EssayQuestionVisibilityCount: quizDump.EssayQuestionVisibilityIds.Count,
            ExamVersionVisibilityCount: quizDump.ExamVersionVisibilityIds.Count,
            StudentProgressCount: progressDump.StudentProgress.Count,
            StudyLogsCount: progressDump.StudyLogs.Count,
            ActivityLogsCount: activityLogs.Count);

        var preparedAtUtc = DateTime.UtcNow;
        var backup = new
        {
            GeneratedAtUtc = preparedAtUtc,
            LopId = lopId,
            LopTen = lop.Ten,
            Note = "File này KHÔNG chứa mật khẩu (PasswordHash) của bất kỳ tài khoản nào — nếu cần " +
                   "khôi phục thủ công 1 tài khoản học viên từ file này, tài khoản đó PHẢI được đặt " +
                   "lại mật khẩu mới (không thể khôi phục mật khẩu cũ).",
            Students = students,
            QuizResults = quizDump.QuizResults,
            ExamResults = quizDump.ExamResults,
            ExamSessions = quizDump.ExamSessions,
            OralResults = quizDump.OralResults,
            WrongAnswers = quizDump.WrongAnswers,
            QuestionVisibilityIds = quizDump.QuestionVisibilityIds,
            EssayQuestionVisibilityIds = quizDump.EssayQuestionVisibilityIds,
            ExamVersionVisibilityIds = quizDump.ExamVersionVisibilityIds,
            StudentProgress = progressDump.StudentProgress,
            StudyLogs = progressDump.StudyLogs,
            ActivityLogs = activityLogs,
        };
        var backupJson = JsonSerializer.Serialize(backup, BackupJsonOptions);

        var preparationId = Guid.NewGuid();
        db.LopDeletionAudits.Add(new LopDeletionAudit
        {
            PreparationId = preparationId,
            AdminUserId = adminUserId,
            LopId = lopId,
            LopTen = lop.Ten,
            Status = LopDeletionAuditStatus.Prepared,
            StudentsCount = counts.StudentsCount,
            QuizResultsCount = counts.QuizResultsCount,
            ExamResultsCount = counts.ExamResultsCount,
            ExamSessionsCount = counts.ExamSessionsCount,
            OralResultsCount = counts.OralResultsCount,
            WrongAnswersCount = counts.WrongAnswersCount,
            QuestionVisibilityCount = counts.QuestionVisibilityCount,
            EssayQuestionVisibilityCount = counts.EssayQuestionVisibilityCount,
            ExamVersionVisibilityCount = counts.ExamVersionVisibilityCount,
            StudentProgressCount = counts.StudentProgressCount,
            StudyLogsCount = counts.StudyLogsCount,
            ActivityLogsCount = counts.ActivityLogsCount,
        });
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Việc 4.2 mục 3 — Admin {AdminUserId} vừa CHUẨN BỊ xóa toàn bộ dữ liệu Lớp {LopId} ({LopTen}), PreparationId={PreparationId}, {StudentsCount} tài khoản học viên.",
            adminUserId, lopId, lop.Ten, preparationId, counts.StudentsCount);

        return new PrepareLopDeletionResponse(preparationId, lopId, lop.Ten, counts, backupJson, preparedAtUtc, preparedAtUtc + PreparationValidity);
    }

    public async Task<ExecuteLopDeletionResponse> ExecuteAsync(Guid lopId, ExecuteLopDeletionRequest request, Guid adminUserId, CancellationToken ct)
    {
        var audit = await db.LopDeletionAudits.FirstOrDefaultAsync(a => a.PreparationId == request.PreparationId, ct)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu chuẩn bị xóa — hãy chuẩn bị lại (tải backup mới).");

        if (audit.LopId != lopId)
        {
            throw new ConflictException("PreparationId không khớp với Lớp đang thao tác.");
        }

        if (audit.Status == LopDeletionAuditStatus.Completed)
        {
            throw new ConflictException("Yêu cầu này đã được thực thi xong trước đó — không thể chạy lại.");
        }

        if (DateTime.UtcNow > audit.PreparedAtUtc + PreparationValidity)
        {
            throw new ConflictException("Yêu cầu chuẩn bị đã hết hiệu lực (quá 30 phút) — hãy chuẩn bị lại (tải backup mới) để đảm bảo số liệu còn đúng.");
        }

        if (!string.Equals(request.ConfirmedLopTen, audit.LopTen, StringComparison.Ordinal))
        {
            throw new ConflictException("Tên Lớp xác nhận không khớp — vui lòng gõ lại chính xác tên Lớp.");
        }

        // Roster phải lấy LẠI (không dùng số liệu Prepare đã cũ) — nếu có học viên mới được thêm
        // vào Lớp giữa lúc Prepare và Execute, vẫn phải xóa đủ, không bỏ sót.
        var students = await authClient.ListHocVienAsync(lopId, ct);
        var userIds = students.Select(s => s.Id).ToList();

        var steps = new List<LopDeletionStepResult>();
        var allSucceeded = true;

        allSucceeded &= await RunStepAsync(steps, "quiz-service", async () =>
        {
            var result = await quizClient.DeleteAsync(userIds, lopId, ct);
            return $"QuizResults={result.QuizResultsDeleted}, ExamResults={result.ExamResultsDeleted}, ExamSessions={result.ExamSessionsDeleted}, " +
                   $"OralResults={result.OralResultsDeleted}, WrongAnswers={result.WrongAnswersDeleted}, " +
                   $"QuestionVisibility={result.QuestionVisibilityDeleted}, EssayQuestionVisibility={result.EssayQuestionVisibilityDeleted}, ExamVersionVisibility={result.ExamVersionVisibilityDeleted}";
        });

        if (allSucceeded)
        {
            allSucceeded &= await RunStepAsync(steps, "progress-service", async () =>
            {
                var result = await progressClient.DeleteAsync(userIds, ct);
                return $"StudentProgress={result.StudentProgressDeleted}, StudyLogs={result.StudyLogsDeleted}";
            });
        }

        if (allSucceeded)
        {
            allSucceeded &= await RunStepAsync(steps, "auth-service", async () =>
            {
                var result = await authClient.DeleteAllLopDataAsync(lopId, ct);
                return $"Users={result.UsersDeleted}, ActivityLogs={result.ActivityLogsDeleted}, LopDeleted={result.LopDeleted}";
            });
        }

        var completedAtUtc = DateTime.UtcNow;
        audit.Status = allSucceeded ? LopDeletionAuditStatus.Completed : LopDeletionAuditStatus.PartialFailure;
        audit.StepResultsJson = JsonSerializer.Serialize(steps);
        audit.CompletedAtUtc = allSucceeded ? completedAtUtc : null;
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "Việc 4.2 mục 3 — Admin {AdminUserId} vừa XÓA toàn bộ dữ liệu Lớp {LopId} ({LopTen}), PreparationId={PreparationId}, Status={Status}.",
            adminUserId, lopId, audit.LopTen, request.PreparationId, audit.Status);

        return new ExecuteLopDeletionResponse(lopId, audit.Status, steps, completedAtUtc);
    }

    private static async Task<bool> RunStepAsync(List<LopDeletionStepResult> steps, string stepName, Func<Task<string>> action)
    {
        try
        {
            var detail = await action();
            steps.Add(new LopDeletionStepResult(stepName, true, detail));
            return true;
        }
        catch (Exception ex)
        {
            steps.Add(new LopDeletionStepResult(stepName, false, ex.Message));
            return false;
        }
    }

    public async Task<IReadOnlyList<LopDeletionAuditResponse>> GetAuditHistoryAsync(int top, CancellationToken ct) =>
        await db.LopDeletionAudits
            .OrderByDescending(a => a.PreparedAtUtc)
            .Take(top)
            .Select(a => new LopDeletionAuditResponse(
                a.Id, a.PreparationId, a.AdminUserId, a.LopId, a.LopTen, a.Status,
                new LopDeletionCounts(
                    a.StudentsCount, a.QuizResultsCount, a.ExamResultsCount, a.ExamSessionsCount, a.OralResultsCount,
                    a.WrongAnswersCount, a.QuestionVisibilityCount, a.EssayQuestionVisibilityCount, a.ExamVersionVisibilityCount,
                    a.StudentProgressCount, a.StudyLogsCount, a.ActivityLogsCount),
                a.PreparedAtUtc, a.CompletedAtUtc))
            .ToListAsync(ct);
}
