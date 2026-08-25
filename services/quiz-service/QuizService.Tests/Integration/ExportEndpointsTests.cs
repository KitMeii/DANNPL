using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

public sealed class ExportEndpointsTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public ExportEndpointsTests(QuizApiFactory factory)
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

    private async Task<QuestionResponse> CreateQuestionAsync(string questionText, int correctAnswer)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateQuestionRequest("1", questionText, "Option A text", "Option B text", "Option C text", "Option D text", correctAnswer, null));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
    }

    private async Task<EssayQuestionResponse> CreateEssayQuestionAsync(string questionText)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateEssayQuestionRequest("1", questionText, "Suggested answer text"));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
    }

    [Fact]
    public async Task Unauthenticated_cannot_export()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/quiz/export/word", new ExportWordRequest([Guid.NewGuid()], []));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_export()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/export/word", TestTokens.Student());
        request.Content = JsonContent.Create(new ExportWordRequest([Guid.NewGuid()], []));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Exporting_with_no_ids_at_all_is_rejected()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/export/word", TestTokens.Teacher());
        request.Content = JsonContent.Create(new ExportWordRequest([], []));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_exports_mixed_mcq_and_essay_into_a_valid_docx_with_correct_content()
    {
        var mcq = await CreateQuestionAsync("Câu hỏi trắc nghiệm xuất Word duy nhất?", correctAnswer: 2);
        var essay = await CreateEssayQuestionAsync("Câu hỏi tự luận xuất Word duy nhất?");

        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/export/word", TestTokens.Teacher());
        request.Content = JsonContent.Create(new ExportWordRequest([mcq.Id], [essay.Id]));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;

        Assert.Contains("Câu hỏi trắc nghiệm xuất Word duy nhất?", text);
        Assert.Contains("Option C text", text); // đáp án đúng (index 2 = C) phải có mặt
        Assert.Contains("Câu hỏi tự luận xuất Word duy nhất?", text);
        Assert.Contains("Suggested answer text", text);
        Assert.Contains("TRẮC NGHIỆM", text);
        Assert.Contains("TỰ LUẬN", text);
    }

    [Fact]
    public async Task Exporting_unknown_ids_produces_an_empty_but_valid_docx()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/export/word", TestTokens.Teacher());
        request.Content = JsonContent.Create(new ExportWordRequest([Guid.NewGuid()], []));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        Assert.NotNull(doc.MainDocumentPart!.Document.Body);
    }

    // Việc 5 (2026-08-16) — export giờ nhận thêm OralQuestionIds (mặc định null/rỗng, không phá
    // vỡ 2 test trên vẫn gọi ExportWordRequest với 2 tham số cũ).
    private async Task<OralQuestionResponse> CreateOralQuestionAsync(string questionText)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral-questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateOralQuestionRequest("1", questionText, "Đáp án chuẩn xuất Word duy nhất", 2));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<OralQuestionResponse>>())!.Data!;
    }

    [Fact]
    public async Task Teacher_exports_oral_questions_into_a_valid_docx_with_correct_content()
    {
        var oral = await CreateOralQuestionAsync("Câu hỏi vấn đáp xuất Word duy nhất?");

        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/export/word", TestTokens.Teacher());
        request.Content = JsonContent.Create(new ExportWordRequest([], [], [oral.Id]));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;

        Assert.Contains("Câu hỏi vấn đáp xuất Word duy nhất?", text);
        Assert.Contains("Đáp án chuẩn xuất Word duy nhất", text);
        Assert.Contains("VẤN ĐÁP", text);
    }
}
