using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdminService.Api.Clients;
using AdminService.Api.Data;
using AdminService.Api.Dtos;
using AdminService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts;
using Shared.Infrastructure.Common;
using Xunit;

namespace AdminService.Tests.Integration;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — "xóa toàn bộ dữ liệu Lớp" (hủy diệt, Admin-only, KHÔNG
/// khôi phục). Trọng tâm: (1) backup PHẢI dựng xong trước khi cho phép xóa, thất bại thì không ghi
/// gì; (2) RBAC chặn Teacher; (3) saga báo lỗi đúng bước và có thể gọi lại an toàn (idempotent);
/// (4) CHỈ đúng Lớp mục tiêu bị xóa — Lớp khác/học viên khác KHÔNG bị đụng tới, kiểm tra bằng kịch
/// bản nhiều Lớp cùng lúc theo đúng yêu cầu test của user.</summary>
public sealed class LopDeletionEndpointsTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;
    private readonly HttpClient _client;

    public LopDeletionEndpointsTests(AdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private Guid SeedLop(string lopTen, out Guid khoaId)
    {
        khoaId = Guid.NewGuid();
        var lopId = Guid.NewGuid();
        _factory.AuthClient.Khoas[khoaId] = new RemoteKhoa(khoaId, $"Khoa-{lopTen}");
        _factory.AuthClient.Lops[lopId] = new RemoteLop(lopId, lopTen, khoaId, null);
        return lopId;
    }

    private Guid SeedStudent(Guid lopId, string name)
    {
        var id = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(id, $"{name}@test.local", name, Roles.Student, LopId: lopId));
        return id;
    }

    private async Task<int> AuditRowCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        return await db.LopDeletionAudits.CountAsync();
    }

    [Fact]
    public async Task Prepare_returns_real_counts_and_downloadable_backup_without_deleting_anything()
    {
        var lopId = SeedLop("Lop-Prepare", out _);
        SeedStudent(lopId, "SV1");
        SeedStudent(lopId, "SV2");
        _factory.QuizLopDataClient.Dump = new RemoteQuizLopDataDump(
            QuizResults: [new RemoteQuizResult(Guid.NewGuid(), Guid.NewGuid(), "C1", 8m, 8, 10, DateTime.UtcNow)],
            ExamResults: [], ExamSessions: [], OralResults: [], WrongAnswers: [],
            QuestionVisibilityIds: [], EssayQuestionVisibilityIds: [], ExamVersionVisibilityIds: []);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!;

        Assert.Equal(2, body.Counts.StudentsCount);
        Assert.Equal(1, body.Counts.QuizResultsCount);
        Assert.NotEqual(Guid.Empty, body.PreparationId);
        Assert.Contains("SV1", body.BackupJson);
        // RemoteUser (kiểu dùng để dựng Students trong backup) không có field PasswordHash — đảm
        // bảo ở tầng compile-time rằng backup không thể vô tình chứa mật khẩu. "Note" (tên field,
        // ASCII) xác nhận ghi chú khôi phục có mặt — không so khớp trực tiếp văn bản tiếng Việt vì
        // JsonSerializer mặc định escape thành \uXXXX, không phải chuỗi UTF-8 thô.
        Assert.Contains("\"Note\"", body.BackupJson);

        // Chưa xóa gì — Lớp và học viên vẫn còn nguyên trong "auth-service" (fake).
        Assert.True(_factory.AuthClient.Lops.ContainsKey(lopId));
        Assert.Equal(2, _factory.AuthClient.Users.Count(u => u.LopId == lopId));
    }

    // PrepareAsync đọc theo thứ tự: GetLopAsync trước tiên — nếu Lớp không tồn tại (hoặc bất kỳ
    // bước đọc/dựng backup nào khác thất bại), hàm ném exception NGAY, KHÔNG ghi audit row nào —
    // đúng yêu cầu "backup thất bại thì không được xóa/ghi gì" (bổ sung an toàn #1).
    [Fact]
    public async Task Prepare_failure_leaves_no_audit_row_and_deletes_nothing()
    {
        var before = await AuditRowCountAsync();

        var response = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{Guid.NewGuid()}/prepare-deletion", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await AuditRowCountAsync());
    }

    [Fact]
    public async Task Teacher_cannot_prepare_or_execute_deletion()
    {
        var lopId = SeedLop("Lop-TeacherBlock", out _);
        var prepareResponse = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Teacher()));
        Assert.Equal(HttpStatusCode.Forbidden, prepareResponse.StatusCode);

        var executeResponse = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Teacher(),
            new { PreparationId = Guid.NewGuid(), ConfirmedLopTen = "x" }));
        Assert.Equal(HttpStatusCode.Forbidden, executeResponse.StatusCode);
    }

    [Fact]
    public async Task Execute_with_wrong_confirmed_ten_is_rejected_and_deletes_nothing()
    {
        var lopId = SeedLop("Lop-WrongTen", out _);
        SeedStudent(lopId, "SV1");

        var prepare = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Admin()));
        var preparationId = (await prepare.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!.PreparationId;

        var execute = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = preparationId, ConfirmedLopTen = "Sai Tên" }));

        Assert.Equal(HttpStatusCode.Conflict, execute.StatusCode);
        Assert.True(_factory.AuthClient.Lops.ContainsKey(lopId));
    }

    [Fact]
    public async Task Execute_with_unknown_preparation_id_returns_not_found()
    {
        var lopId = SeedLop("Lop-UnknownPrep", out _);
        var response = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = Guid.NewGuid(), ConfirmedLopTen = "Lop-UnknownPrep" }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Trọng tâm test theo đúng yêu cầu: nhiều Lớp cùng lúc, xóa 1 Lớp mục tiêu — Lớp khác + học
    // viên của Lớp khác + GV chủ nhiệm KHÔNG bị đụng tới.
    [Fact]
    public async Task Execute_deletes_only_the_target_lop_leaving_other_lop_and_teacher_intact()
    {
        var targetLopId = SeedLop("Lop-Target", out _);
        var otherLopId = SeedLop("Lop-Other", out _);
        var teacherId = Guid.NewGuid();
        _factory.AuthClient.Lops[targetLopId] = _factory.AuthClient.Lops[targetLopId] with { GiaoVienId = teacherId };
        _factory.AuthClient.Users.Add(new RemoteUser(teacherId, "gv@test.local", "GV Chu Nhiem", Roles.Teacher));

        var targetStudent1 = SeedStudent(targetLopId, "Target-SV1");
        var targetStudent2 = SeedStudent(targetLopId, "Target-SV2");
        var otherStudent = SeedStudent(otherLopId, "Other-SV1");

        _factory.QuizLopDataClient.Dump = new RemoteQuizLopDataDump(
            QuizResults: [new RemoteQuizResult(Guid.NewGuid(), targetStudent1, "C1", 8m, 8, 10, DateTime.UtcNow)],
            ExamResults: [], ExamSessions: [], OralResults: [], WrongAnswers: [],
            QuestionVisibilityIds: [], EssayQuestionVisibilityIds: [], ExamVersionVisibilityIds: []);
        _factory.ProgressLopDataClient.Dump = new RemoteProgressLopDataDump(
            StudentProgress: [new RemoteStudentProgress(targetStudent1, 3, null, 90, 10, 70m, DateTime.UtcNow)],
            StudyLogs: []);

        var prepare = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{targetLopId}/prepare-deletion", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.OK, prepare.StatusCode);
        var prepareBody = (await prepare.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!;
        Assert.Equal(2, prepareBody.Counts.StudentsCount);

        var execute = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{targetLopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = prepareBody.PreparationId, ConfirmedLopTen = "Lop-Target" }));

        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);
        var executeBody = (await execute.Content.ReadFromJsonAsync<ApiResponse<ExecuteLopDeletionResponse>>())!.Data!;
        Assert.Equal(LopDeletionAuditStatus.Completed, executeBody.Status);
        Assert.All(executeBody.Steps, s => Assert.True(s.Success));

        // Lớp mục tiêu + 2 học viên của nó biến mất.
        Assert.False(_factory.AuthClient.Lops.ContainsKey(targetLopId));
        Assert.DoesNotContain(_factory.AuthClient.Users, u => u.Id == targetStudent1 || u.Id == targetStudent2);

        // Lớp KHÁC + học viên của lớp khác + GV chủ nhiệm KHÔNG bị đụng.
        Assert.True(_factory.AuthClient.Lops.ContainsKey(otherLopId));
        Assert.Contains(_factory.AuthClient.Users, u => u.Id == otherStudent);
        Assert.Contains(_factory.AuthClient.Users, u => u.Id == teacherId);

        // quiz-service/progress-service chỉ nhận đúng UserIds của Lớp mục tiêu (không lẫn otherStudent).
        Assert.NotNull(_factory.QuizLopDataClient.LastDeleteCall);
        Assert.Equal(targetLopId, _factory.QuizLopDataClient.LastDeleteCall!.Value.LopId);
        Assert.Equal(
            new[] { targetStudent1, targetStudent2 }.OrderBy(x => x),
            _factory.QuizLopDataClient.LastDeleteCall!.Value.UserIds.OrderBy(x => x));
        Assert.DoesNotContain(otherStudent, _factory.QuizLopDataClient.LastDeleteCall!.Value.UserIds);
    }

    [Fact]
    public async Task Executing_the_same_preparation_twice_is_rejected_the_second_time()
    {
        var lopId = SeedLop("Lop-DoubleExec", out _);
        SeedStudent(lopId, "SV1");

        var prepare = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Admin()));
        var preparationId = (await prepare.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!.PreparationId;

        var first = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = preparationId, ConfirmedLopTen = "Lop-DoubleExec" }));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = preparationId, ConfirmedLopTen = "Lop-DoubleExec" }));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // Saga phải dừng ĐÚNG ở bước lỗi, báo cáo rõ bước nào lỗi, KHÔNG chạy tiếp các bước sau (idempotent
    // — có thể gọi lại an toàn) — đúng yêu cầu test của user.
    [Fact]
    public async Task Saga_reports_correct_partial_failure_when_quiz_service_is_interrupted_and_retry_succeeds()
    {
        var lopId = SeedLop("Lop-PartialFail", out _);
        SeedStudent(lopId, "SV1");

        // Đếm delta thay vì số tuyệt đối — AdminApiFactory (IClassFixture) dùng CHUNG 1
        // FakeAuthAdminClient/FakeQuizLopDataClient/FakeProgressLopDataClient cho toàn bộ các test
        // trong class này, nên các bộ đếm gọi (CallCount) cộng dồn qua nhiều test khác chạy trước.
        var progressCallsBefore = _factory.ProgressLopDataClient.DeleteCallCount;
        var authCallsBefore = _factory.AuthClient.DeleteAllLopDataCallCount;

        _factory.QuizLopDataClient.DeleteFailure = new InvalidOperationException("quiz-service tạm thời gián đoạn (mô phỏng).");

        var prepare1 = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Admin()));
        var preparationId1 = (await prepare1.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!.PreparationId;

        var execute1 = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = preparationId1, ConfirmedLopTen = "Lop-PartialFail" }));

        Assert.Equal(HttpStatusCode.OK, execute1.StatusCode);
        var body1 = (await execute1.Content.ReadFromJsonAsync<ApiResponse<ExecuteLopDeletionResponse>>())!.Data!;
        Assert.Equal(LopDeletionAuditStatus.PartialFailure, body1.Status);
        Assert.False(body1.Steps.Single(s => s.Step == "quiz-service").Success);

        // Vì quiz-service (bước ĐẦU) thất bại, progress-service và auth-service KHÔNG được gọi tới —
        // thứ tự saga (dữ liệu phụ thuộc trước, tài khoản sau) được tôn trọng, không xóa lung tung.
        Assert.Equal(progressCallsBefore, _factory.ProgressLopDataClient.DeleteCallCount);
        Assert.Equal(authCallsBefore, _factory.AuthClient.DeleteAllLopDataCallCount);
        Assert.True(_factory.AuthClient.Lops.ContainsKey(lopId));

        // Retry: sửa lỗi quiz-service rồi chuẩn bị + thực thi lại — phải thành công, an toàn (không
        // xóa trùng, không lỗi "đã xóa rồi").
        _factory.QuizLopDataClient.DeleteFailure = null;

        var prepare2 = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/prepare-deletion", TestTokens.Admin()));
        var preparationId2 = (await prepare2.Content.ReadFromJsonAsync<ApiResponse<PrepareLopDeletionResponse>>())!.Data!.PreparationId;

        var execute2 = await _client.SendAsync(WithAuth(HttpMethod.Post, $"/api/v1/admin/lop/{lopId}/execute-deletion", TestTokens.Admin(),
            new { PreparationId = preparationId2, ConfirmedLopTen = "Lop-PartialFail" }));

        Assert.Equal(HttpStatusCode.OK, execute2.StatusCode);
        var body2 = (await execute2.Content.ReadFromJsonAsync<ApiResponse<ExecuteLopDeletionResponse>>())!.Data!;
        Assert.Equal(LopDeletionAuditStatus.Completed, body2.Status);
        Assert.False(_factory.AuthClient.Lops.ContainsKey(lopId));
    }
}
