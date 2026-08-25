using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

public sealed class ExamSetEndpointsTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public ExamSetEndpointsTests(QuizApiFactory factory)
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

    private async Task<List<Guid>> CreatePoolAsync(int count, string chapterPrefix)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var difficulty = (i % 3) + 1;
            var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", TestTokens.Teacher());
            request.Content = JsonContent.Create(new CreateQuestionRequest(
                $"{chapterPrefix}", $"Câu {i}?", "A", "B", "C", "D", 0, null,
                SourceType: "Imported", SourceMaterialId: null, Difficulty: difficulty, Topic: $"topic-{i % 4}"));
            var response = await _client.SendAsync(request);
            var body = (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
            ids.Add(body.Id);
        }
        return ids;
    }

    [Fact]
    public async Task Student_cannot_generate_exam_set_versions()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", TestTokens.Student());
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Test", [Guid.NewGuid()], null, 1, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_cannot_list_exam_sets()
    {
        var response = await _client.GetAsync("/api/v1/quiz/exam-sets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Version_count_outside_2_to_4_is_rejected()
    {
        var pool = await CreatePoolAsync(20, "ExamSet-vcount-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bad version count", pool, null, 10, 5));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Target_count_larger_than_pool_is_rejected()
    {
        var pool = await CreatePoolAsync(5, "ExamSet-toobig-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Target too big", pool, null, 10, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_generates_distinct_versions_with_correct_ma_de_and_target_count()
    {
        var pool = await CreatePoolAsync(150, "ExamSet-150-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề 150→50 test", pool, null, 50, 3));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        Assert.Equal(150, body.TotalPoolSize);
        Assert.Equal(3, body.Versions.Count);
        Assert.Equal(["101", "102", "103"], body.Versions.Select(v => v.MaDe));

        foreach (var version in body.Versions)
        {
            Assert.Equal(50, version.Questions.Count);
        }

        // Không mã nào giống hệt mã khác.
        var sets = body.Versions.Select(v => v.Questions.Select(q => q.Id).ToHashSet()).ToList();
        for (var i = 0; i < sets.Count; i++)
        {
            for (var j = i + 1; j < sets.Count; j++)
            {
                Assert.False(sets[i].SetEquals(sets[j]), $"Mã đề {body.Versions[i].MaDe} và {body.Versions[j].MaDe} có tập câu hỏi giống hệt nhau.");
            }
        }
    }

    [Fact]
    public async Task Generated_exam_set_is_retrievable_via_list_and_get_by_id()
    {
        // Rà soát Lần XI (2026-08-21) — CÙNG 1 giáo viên tạo/list/xem chi tiết (ExamSet giờ kiểm
        // ownership, 2 lần gọi TestTokens.Teacher() riêng sẽ sinh 2 userId khác nhau).
        var teacherToken = TestTokens.Teacher();
        var pool = await CreatePoolAsync(30, "ExamSet-retrieve-test");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherToken);
        createRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề retrieve test", pool, null, 10, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/exam-sets", teacherToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ExamSetSummaryResponse>>>())!.Data!;
        Assert.Contains(list, s => s.Id == created.Id && s.VersionCount == 2);

        var detailRequest = WithAuth(HttpMethod.Get, $"/api/v1/quiz/exam-sets/{created.Id}", teacherToken);
        var detailResponse = await _client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        Assert.Equal(2, detail.Versions.Count);
        Assert.All(detail.Versions, v => Assert.Equal(10, v.Questions.Count));
    }

    [Fact]
    public async Task Getting_an_unknown_exam_set_returns_404()
    {
        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/exam-sets/{Guid.NewGuid()}", TestTokens.Teacher());
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════ Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng có sẵn ═══════════════

    private async Task<List<Guid>> CreateOralPoolAsync(int count, string chapterPrefix)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral-questions", TestTokens.Teacher());
            request.Content = JsonContent.Create(new CreateOralQuestionRequest(chapterPrefix, $"Câu vấn đáp {i}?", "Đáp án chuẩn", 2));
            var response = await _client.SendAsync(request);
            var body = (await response.Content.ReadFromJsonAsync<ApiResponse<OralQuestionResponse>>())!.Data!;
            ids.Add(body.Id);
        }
        return ids;
    }

    [Fact]
    public async Task Student_cannot_generate_oral_exam_set_versions()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", TestTokens.Student());
        request.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Test VĐ", [Guid.NewGuid()], 1, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Oral_target_count_outside_1_to_4_is_rejected()
    {
        var pool = await CreateOralPoolAsync(6, "Oral-count-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Bad target count", pool, 5, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oral_target_count_larger_than_pool_is_rejected()
    {
        var pool = await CreateOralPoolAsync(2, "Oral-toobig-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Target too big", pool, 4, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_generates_oral_exam_set_with_correct_kind_and_target_count()
    {
        var pool = await CreateOralPoolAsync(10, "Oral-happy-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Bộ đề VĐ test", pool, 3, 2));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        Assert.Equal(10, body.TotalPoolSize);
        Assert.Equal(2, body.Versions.Count);
        Assert.All(body.Versions, v =>
        {
            Assert.Equal("Oral", v.Kind);
            Assert.Equal(3, v.OralQuestions.Count);
            Assert.Empty(v.Questions);
        });
    }

    [Fact]
    public async Task Generated_oral_exam_set_is_retrievable_via_get_by_id_with_oral_questions_populated()
    {
        // Rà soát Lần XI (2026-08-21) — CÙNG 1 giáo viên tạo/xem chi tiết (cùng lý do test trên).
        var teacherToken = TestTokens.Teacher();
        var pool = await CreateOralPoolAsync(5, "Oral-retrieve-test");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", teacherToken);
        createRequest.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Bộ đề VĐ retrieve test", pool, 2, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        var detailRequest = WithAuth(HttpMethod.Get, $"/api/v1/quiz/exam-sets/{created.Id}", teacherToken);
        var detailResponse = await _client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        Assert.All(detail.Versions, v =>
        {
            Assert.Equal("Oral", v.Kind);
            Assert.Equal(2, v.OralQuestions.Count);
            Assert.All(v.OralQuestions, q => Assert.NotEmpty(q.QuestionText));
        });
    }

    [Fact]
    public async Task Publishing_an_oral_version_is_rejected()
    {
        // Rà soát Lần XI (2026-08-21) — CÙNG 1 giáo viên tạo/publish (cùng lý do test trên).
        var teacherToken = TestTokens.Teacher();
        var pool = await CreateOralPoolAsync(3, "Oral-publish-test");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate-oral", teacherToken);
        createRequest.Content = JsonContent.Create(new GenerateOralExamSetVersionsRequest("Bộ đề VĐ publish test", pool, 2, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        var versionId = created.Versions[0].Id;

        var publishRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/publish", teacherToken);
        var publishResponse = await _client.SendAsync(publishRequest);

        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
    }

    [Fact]
    public async Task Generated_mcq_exam_set_versions_have_kind_mcq()
    {
        var pool = await CreatePoolAsync(15, "ExamSet-kind-test");
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", TestTokens.Teacher());
        request.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề TN kind test", pool, null, 10, 2));

        var response = await _client.SendAsync(request);

        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        Assert.All(body.Versions, v =>
        {
            Assert.Equal("Mcq", v.Kind);
            Assert.Empty(v.OralQuestions);
        });
    }

    // ═══════════════ Rà soát Lần XI (2026-08-21) — GV chỉ thấy/thao tác Bộ đề CHÍNH MÌNH tạo ═══
    // (cùng lỗi đã sửa cho Question/OralQuestion/Material ở Lần VIII, ExamSet/ExamVersion bị bỏ sót).

    [Fact]
    public async Task Teacher_B_does_not_see_teacher_A_exam_set_in_list()
    {
        var teacherAToken = TestTokens.Teacher();
        var teacherBToken = TestTokens.Teacher();

        var pool = await CreatePoolAsync(15, "ExamSet-isolation-list");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherAToken);
        createRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề GV A", pool, null, 10, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/exam-sets", teacherBToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ExamSetSummaryResponse>>>())!.Data!;
        Assert.DoesNotContain(list, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Teacher_B_cannot_view_teacher_A_exam_set_detail()
    {
        var teacherAToken = TestTokens.Teacher();
        var teacherBToken = TestTokens.Teacher();

        var pool = await CreatePoolAsync(15, "ExamSet-isolation-detail");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherAToken);
        createRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề GV A chi tiết", pool, null, 10, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        var detailRequest = WithAuth(HttpMethod.Get, $"/api/v1/quiz/exam-sets/{created.Id}", teacherBToken);
        var detailResponse = await _client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.Forbidden, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Teacher_B_cannot_publish_or_unpublish_teacher_A_exam_version()
    {
        var teacherAToken = TestTokens.Teacher();
        var teacherBToken = TestTokens.Teacher();

        var pool = await CreatePoolAsync(15, "ExamSet-isolation-publish");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherAToken);
        createRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề GV A publish", pool, null, 10, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        var versionId = created.Versions[0].Id;

        var publishByB = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/publish", teacherBToken));
        Assert.Equal(HttpStatusCode.Forbidden, publishByB.StatusCode);

        var publishByA = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/publish", teacherAToken));
        Assert.Equal(HttpStatusCode.OK, publishByA.StatusCode);

        var unpublishByB = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/unpublish", teacherBToken));
        Assert.Equal(HttpStatusCode.Forbidden, unpublishByB.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_and_view_any_teacher_exam_set()
    {
        var teacherToken = TestTokens.Teacher();
        var adminToken = TestTokens.Admin();

        var pool = await CreatePoolAsync(15, "ExamSet-admin-oversight");
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherToken);
        createRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Bộ đề GV cho Admin xem", pool, null, 10, 2));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        var listResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/exam-sets", adminToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ExamSetSummaryResponse>>>())!.Data!;
        Assert.Contains(list, s => s.Id == created.Id);

        var detailResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/quiz/exam-sets/{created.Id}", adminToken));
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }
}
