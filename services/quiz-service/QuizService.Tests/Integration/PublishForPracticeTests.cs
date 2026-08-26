using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

public sealed class PublishForPracticeTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    // Rà soát Lần VIII (2026-08-21) — cố định 1 danh tính GV cho cả lớp test, cùng lý do
    // CascadeDeleteGuardTests: tạo câu hỏi và publish/unpublish nó phải LÀ ĐÚNG 1 giáo viên sau
    // khi thêm kiểm tra ownership, không phải 2 lần gọi _teacherToken sinh 2 userId khác nhau.
    private readonly string _teacherToken = TestTokens.Teacher();

    public PublishForPracticeTests(QuizApiFactory factory)
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

    private async Task<QuestionResponse> CreateQuestionAsync(string chapter, string sourceType = "Manual")
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", _teacherToken);
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            chapter, $"Q {Guid.NewGuid()}?", "A", "B", "C", "D", 0, null, SourceType: sourceType));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
    }

    private async Task<EssayQuestionResponse> CreateEssayQuestionAsync(string chapter, string sourceType = "Manual")
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", _teacherToken);
        request.Content = JsonContent.Create(new CreateEssayQuestionRequest(chapter, $"Q {Guid.NewGuid()}?", "Gợi ý", SourceType: sourceType));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
    }

    // ── Default publish state by SourceType ──

    [Fact]
    public async Task Manual_question_is_published_by_default()
    {
        var q = await CreateQuestionAsync("Publish-manual-default");
        Assert.True(q.IsPublished);

        var practice = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/published?chapter=Publish-manual-default", TestTokens.Student()));
        var list = (await practice.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
        Assert.Contains(list, x => x.Id == q.Id);
    }

    [Fact]
    public async Task AiGenerated_question_is_not_published_by_default_and_hidden_from_practice()
    {
        var q = await CreateQuestionAsync("Publish-ai-default", sourceType: "AiGenerated");
        Assert.False(q.IsPublished);

        var practice = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/published?chapter=Publish-ai-default", TestTokens.Student()));
        var list = (await practice.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
        Assert.DoesNotContain(list, x => x.Id == q.Id);
    }

    [Fact]
    public async Task Imported_essay_question_is_not_published_by_default()
    {
        var q = await CreateEssayQuestionAsync("Publish-imported-essay", sourceType: "Imported");
        Assert.False(q.IsPublishedForPractice);
    }

    // ── Toggle publish ──

    [Fact]
    public async Task Teacher_can_publish_an_ai_generated_question_and_it_then_appears_in_practice()
    {
        var q = await CreateQuestionAsync("Publish-toggle-test", sourceType: "AiGenerated");

        var toggleRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{q.Id}/publish", _teacherToken);
        var toggleResponse = await _client.SendAsync(toggleRequest);
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);
        var toggled = (await toggleResponse.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
        Assert.True(toggled.IsPublished);

        var practice = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/published?chapter=Publish-toggle-test", TestTokens.Student()));
        var list = (await practice.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
        Assert.Contains(list, x => x.Id == q.Id);

        // Toggle lần 2 phải gỡ xuất bản.
        var untoggleResponse = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{q.Id}/publish", _teacherToken));
        var untoggled = (await untoggleResponse.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
        Assert.False(untoggled.IsPublished);
    }

    [Fact]
    public async Task Teacher_can_publish_an_essay_question()
    {
        var q = await CreateEssayQuestionAsync("Publish-essay-toggle-test", sourceType: "AiGenerated");
        Assert.False(q.IsPublishedForPractice);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{q.Id}/publish", _teacherToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var toggled = (await response.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
        Assert.True(toggled.IsPublishedForPractice);
    }

    [Fact]
    public async Task Student_cannot_toggle_publish_on_question_or_essay_question()
    {
        var q = await CreateQuestionAsync("Publish-rbac-question");
        var eq = await CreateEssayQuestionAsync("Publish-rbac-essay");

        var r1 = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{q.Id}/publish", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode);

        var r2 = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/essay-questions/{eq.Id}/publish", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, r2.StatusCode);
    }

    [Fact]
    public async Task Publishing_an_unknown_question_returns_404()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{Guid.NewGuid()}/publish", _teacherToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── ExamVersion bulk publish ──

    [Fact]
    public async Task Publishing_an_exam_version_publishes_every_question_in_it()
    {
        var poolIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var q = await CreateQuestionAsync("Publish-examversion-pool", sourceType: "AiGenerated");
            poolIds.Add(q.Id);
        }

        var genRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", _teacherToken);
        genRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Publish version test", poolIds, null, 4, 2));
        var genResponse = await _client.SendAsync(genRequest);
        var examSet = (await genResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        var version = examSet.Versions[0];

        // Trước khi publish: chưa câu nào trong mã đề này xuất hiện ở luyện tập.
        var beforePractice = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/published?chapter=Publish-examversion-pool", TestTokens.Student()));
        var beforeList = (await beforePractice.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
        Assert.DoesNotContain(version.Questions, vq => beforeList.Any(b => b.Id == vq.Id));

        var publishResponse = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{version.Id}/publish", _teacherToken));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = (await publishResponse.Content.ReadFromJsonAsync<ApiResponse<PublishVersionResponse>>())!.Data!;
        Assert.Equal(version.Questions.Count, published.PublishedCount);

        var afterPractice = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/published?chapter=Publish-examversion-pool", TestTokens.Student()));
        var afterList = (await afterPractice.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
        Assert.All(version.Questions, vq => Assert.Contains(afterList, a => a.Id == vq.Id));
    }

    [Fact]
    public async Task Student_cannot_publish_an_exam_version()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{Guid.NewGuid()}/publish", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Publishing_an_unknown_exam_version_returns_404()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{Guid.NewGuid()}/publish", _teacherToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
