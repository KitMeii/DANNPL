using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Audit "Việc 1" — ExamVersionQuestion.QuestionId là Cascade ở tầng DB; nếu không chặn ở
/// application layer, xóa 1 Question thuộc mã đề ĐÃ XUẤT BẢN sẽ âm thầm làm mã đề co lại (đã xác
/// nhận qua test thật với API trước khi vá — xem báo cáo). Các test này khoá lại hành vi ĐÚNG sau
/// khi vá: chặn (409) khi mã đề đã publish, vẫn cho xóa tự do khi mã đề chưa publish hoặc câu hỏi
/// không thuộc mã đề nào.</summary>
public sealed class CascadeDeleteGuardTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    // Rà soát Lần VIII (2026-08-21) — CỐ ĐỊNH 1 danh tính GV cho cả lớp test này (trước mỗi hàm
    // helper tự gọi TestTokens.Teacher() riêng, sinh userId NGẪU NHIÊN mỗi lần — vô tình khiến
    // "người tạo câu hỏi" và "người xóa/publish" luôn khác nhau. Sau khi thêm kiểm tra ownership
    // (chỉ người tạo/Admin được sửa/xóa câu hỏi), các test này bắt đầu nhận 403 sai chỗ dù không
    // hề kiểm tra ownership — sửa bằng cách dùng ĐÚNG 1 giáo viên xuyên suốt, đúng tinh thần các
    // test này (kiểm cascade-delete-guard/publish, không phải kiểm ownership).
    private readonly string _teacherToken = TestTokens.Teacher();

    public CascadeDeleteGuardTests(QuizApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<Guid> CreateQuestionAsync(string chapter)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", _teacherToken);
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            chapter, $"Q {Guid.NewGuid()}?", "A", "B", "C", "D", 0, null, SourceType: "AiGenerated"));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!.Id;
    }

    private async Task<ExamSetResponse> GenerateExamSetAsync(string ten, List<Guid> pool, int targetCount, int versionCount)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", _teacherToken);
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest(ten, pool, null, targetCount, versionCount));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
    }

    private async Task<HttpResponseMessage> PublishVersionAsync(Guid versionId) =>
        await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/publish", _teacherToken));

    private async Task<HttpResponseMessage> UnpublishVersionAsync(Guid versionId) =>
        await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/unpublish", _teacherToken));

    private async Task<HttpResponseMessage> DeleteQuestionAsync(Guid id) =>
        await _client.SendAsync(WithAuth(HttpMethod.Delete, $"/api/v1/quiz/questions/{id}", _teacherToken));

    [Fact]
    public async Task Deleting_a_question_with_no_exam_version_at_all_still_works_normally()
    {
        var id = await CreateQuestionAsync("Guard-no-version");
        var response = await DeleteQuestionAsync(id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_question_that_belongs_only_to_an_unpublished_version_still_works()
    {
        var pool = new List<Guid>();
        for (var i = 0; i < 3; i++) pool.Add(await CreateQuestionAsync("Guard-unpublished-version"));

        var examSet = await GenerateExamSetAsync("Guard unpublished test", pool, 3, 2);
        // Không publish gì cả.

        var response = await DeleteQuestionAsync(examSet.Versions[0].Questions[0].Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_question_belonging_to_a_published_version_is_blocked_with_409_naming_the_ma_de()
    {
        var pool = new List<Guid>();
        for (var i = 0; i < 3; i++) pool.Add(await CreateQuestionAsync("Guard-published-block"));

        var examSet = await GenerateExamSetAsync("Guard published block test", pool, 3, 2);
        var version101 = examSet.Versions[0];
        await PublishVersionAsync(version101.Id);

        var target = version101.Questions[0].Id;
        var response = await DeleteQuestionAsync(target);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(version101.MaDe, body);

        // Câu hỏi vẫn còn nguyên — chưa bị xóa.
        var stillExists = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions?chapter=Guard-published-block", _teacherToken));
        var list = (await stillExists.Content.ReadFromJsonAsync<ApiResponse<List<QuestionResponse>>>())!.Data!;
        Assert.Contains(list, q => q.Id == target);
    }

    [Fact]
    public async Task Unpublishing_the_version_first_makes_the_previously_blocked_delete_succeed()
    {
        var pool = new List<Guid>();
        for (var i = 0; i < 3; i++) pool.Add(await CreateQuestionAsync("Guard-unpublish-then-delete"));

        var examSet = await GenerateExamSetAsync("Guard unpublish-then-delete test", pool, 3, 2);
        var version101 = examSet.Versions[0];
        await PublishVersionAsync(version101.Id);
        var target = version101.Questions[0].Id;

        var blockedResponse = await DeleteQuestionAsync(target);
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);

        var unpublishResponse = await UnpublishVersionAsync(version101.Id);
        Assert.Equal(HttpStatusCode.OK, unpublishResponse.StatusCode);

        var deleteAfterUnpublish = await DeleteQuestionAsync(target);
        Assert.Equal(HttpStatusCode.OK, deleteAfterUnpublish.StatusCode);
    }

    [Fact]
    public async Task Unpublish_does_not_incorrectly_unpublish_a_question_still_published_via_another_version()
    {
        // 1 câu duy nhất trong pool nhỏ (targetCount=pool.Count) buộc cả 2 mã đề đều chứa nó.
        var pool = new List<Guid> { await CreateQuestionAsync("Guard-overlap-test") };

        var examSet = await GenerateExamSetAsync("Guard overlap test", pool, 1, 2);
        var v1 = examSet.Versions[0];
        var v2 = examSet.Versions[1];
        Assert.Equal(v1.Questions[0].Id, v2.Questions[0].Id); // cùng 1 câu, pool chỉ có 1

        await PublishVersionAsync(v1.Id);
        await PublishVersionAsync(v2.Id);

        // Hủy xuất bản v1 — câu này vẫn phải còn published vì v2 vẫn đang publish.
        await UnpublishVersionAsync(v1.Id);

        var afterUnpublishV1 = await DeleteQuestionAsync(pool[0]);
        Assert.Equal(HttpStatusCode.Conflict, afterUnpublishV1.StatusCode); // vẫn bị chặn nhờ v2

        // Hủy nốt v2 — giờ mới thật sự không còn mã đề nào publish câu này.
        await UnpublishVersionAsync(v2.Id);
        var afterUnpublishBoth = await DeleteQuestionAsync(pool[0]);
        Assert.Equal(HttpStatusCode.OK, afterUnpublishBoth.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_unpublish_a_version()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{Guid.NewGuid()}/unpublish", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unpublishing_an_unknown_version_returns_404()
    {
        var response = await UnpublishVersionAsync(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
