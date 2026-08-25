using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

public sealed class EssayQuestionAndSourceTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public EssayQuestionAndSourceTests(QuizApiFactory factory)
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

    // ── Question.SourceType / SourceMaterialId ──

    [Fact]
    public async Task Creating_a_question_without_source_fields_defaults_to_manual()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateQuestionRequest("1", "Câu hỏi thường?", "A", "B", "C", "D", 0, null));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>();
        Assert.Equal("Manual", body!.Data!.SourceType);
        Assert.Null(body.Data.SourceMaterialId);
    }

    [Fact]
    public async Task Creating_a_question_with_ai_generated_source_persists_and_lists_it()
    {
        // Rà soát Lần VIII (2026-08-21) — CÙNG 1 giáo viên tạo lẫn list (ngân hàng câu hỏi giờ lọc
        // theo người tạo, 2 lần gọi TestTokens.Teacher() riêng sẽ sinh 2 userId khác nhau).
        var teacherToken = TestTokens.Teacher();
        var materialId = Guid.NewGuid();
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", teacherToken);
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            "1 (source test)", "Câu hỏi AI sinh?", "A", "B", "C", "D", 1, "giải thích",
            SourceType: "AiGenerated", SourceMaterialId: materialId));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
        Assert.Equal("AiGenerated", created.SourceType);
        Assert.Equal(materialId, created.SourceMaterialId);

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/questions?chapter=1%20(source%20test)", teacherToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<QuestionResponse>>>())!.Data!;
        var found = Assert.Single(list, q => q.Id == created.Id);
        Assert.Equal("AiGenerated", found.SourceType);
        Assert.Equal(materialId, found.SourceMaterialId);
    }

    [Fact]
    public async Task Invalid_source_type_is_rejected()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            "1", "Câu hỏi?", "A", "B", "C", "D", 0, null, SourceType: "NotARealSourceType"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── EssayQuestion CRUD + RBAC ──

    [Fact]
    public async Task Student_cannot_create_essay_question()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", TestTokens.Student());
        request.Content = JsonContent.Create(new CreateEssayQuestionRequest("1", "Trình bày...", "Gợi ý"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_cannot_list_essay_question_bank()
    {
        var response = await _client.GetAsync("/api/v1/quiz/essay-questions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_can_create_update_and_delete_an_essay_question()
    {
        // Rà soát Lần XI (2026-08-21) — CÙNG 1 giáo viên tạo/sửa/xóa (EssayQuestion giờ kiểm
        // ownership, 2 lần gọi TestTokens.Teacher() riêng sẽ sinh 2 userId khác nhau).
        var teacherToken = TestTokens.Teacher();
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("2 (crud test)", "Trình bày quan điểm...", "Gợi ý đáp án"));
        var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
        Assert.Equal("Manual", created.SourceType);

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{created.Id}", teacherToken);
        updateRequest.Content = JsonContent.Create(new UpdateEssayQuestionRequest("2 (crud test)", "Trình bày quan điểm đã sửa...", "Gợi ý đã sửa"));
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
        Assert.Equal("Trình bày quan điểm đã sửa...", updated.QuestionText);

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/quiz/essay-questions/{created.Id}", teacherToken);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Practice_endpoint_never_exposes_suggested_answer()
    {
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", TestTokens.Teacher());
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("3 (practice test)", "Câu hỏi tự luận bí mật?", "Đáp án bí mật tuyệt đối"));
        await _client.SendAsync(createRequest);

        var practiceRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/essay-questions/practice?chapter=3%20(practice%20test)", TestTokens.Student());
        var practiceResponse = await _client.SendAsync(practiceRequest);
        var raw = await practiceResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, practiceResponse.StatusCode);
        Assert.DoesNotContain("Đáp án bí mật tuyệt đối", raw);
        Assert.DoesNotContain("suggestedAnswer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Essay_question_created_with_ai_generated_source_persists_material_link()
    {
        var materialId = Guid.NewGuid();
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateEssayQuestionRequest(
            "4 (source test)", "Câu hỏi AI sinh từ tài liệu?", "Gợi ý", SourceType: "AiGenerated", SourceMaterialId: materialId));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>();
        Assert.Equal("AiGenerated", body!.Data!.SourceType);
        Assert.Equal(materialId, body.Data.SourceMaterialId);
    }

    // ═══ Rà soát Lần XI (2026-08-21) — GV chỉ thấy/sửa/xóa/publish câu tự luận CHÍNH MÌNH tạo ═══
    // (cùng lỗi đã sửa cho Question/OralQuestion/Material ở Lần VIII, EssayQuestion bị bỏ sót).

    [Fact]
    public async Task Teacher_B_does_not_see_teacher_A_essay_question_in_list()
    {
        var teacherAToken = TestTokens.Teacher();
        var teacherBToken = TestTokens.Teacher();

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", teacherAToken);
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("5 (isolation test)", "Câu hỏi GV A?", "Gợi ý"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/essay-questions?chapter=5%20(isolation%20test)", teacherBToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<EssayQuestionResponse>>>())!.Data!;
        Assert.DoesNotContain(list, q => q.Id == created.Id);
    }

    [Fact]
    public async Task Teacher_B_cannot_update_delete_or_publish_teacher_A_essay_question()
    {
        var teacherAToken = TestTokens.Teacher();
        var teacherBToken = TestTokens.Teacher();

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", teacherAToken);
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("6 (isolation test)", "Câu hỏi GV A?", "Gợi ý"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;

        var updateByB = WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{created.Id}", teacherBToken);
        updateByB.Content = JsonContent.Create(new UpdateEssayQuestionRequest("6 (isolation test)", "GV B cố sửa...", "Gợi ý"));
        var updateResponse = await _client.SendAsync(updateByB);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        var publishByB = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{created.Id}/publish", teacherBToken));
        Assert.Equal(HttpStatusCode.Forbidden, publishByB.StatusCode);

        var deleteByB = await _client.SendAsync(WithAuth(HttpMethod.Delete, $"/api/v1/quiz/essay-questions/{created.Id}", teacherBToken));
        Assert.Equal(HttpStatusCode.Forbidden, deleteByB.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_and_update_any_teacher_essay_question()
    {
        var teacherToken = TestTokens.Teacher();
        var adminToken = TestTokens.Admin();

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("7 (admin oversight)", "Câu hỏi GV?", "Gợi ý"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;

        var listResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/essay-questions?chapter=7%20(admin%20oversight)", adminToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<EssayQuestionResponse>>>())!.Data!;
        Assert.Contains(list, q => q.Id == created.Id);

        var updateByAdmin = WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{created.Id}", adminToken);
        updateByAdmin.Content = JsonContent.Create(new UpdateEssayQuestionRequest("7 (admin oversight)", "Admin sửa được...", "Gợi ý"));
        var updateResponse = await _client.SendAsync(updateByAdmin);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }
}
