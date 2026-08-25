using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Việc 4.4 Phần A (2026-08-20) — vá gap: câu hỏi Vấn đáp trước đây không có cơ chế giới
/// hạn Lớp. Song song LopVisibilityTests (Việc 8, dành cho Question/EssayQuestion) — cùng công thức
/// bao phủ cho OralQuestion.</summary>
public sealed class OralLopVisibilityTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public OralLopVisibilityTests(QuizApiFactory factory)
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

    private async Task<OralQuestionResponse> CreateOralQuestionAsync(string token, string chapter, List<Guid>? lopIds = null)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral-questions", token);
        request.Content = JsonContent.Create(new CreateOralQuestionRequest(chapter, $"Q {Guid.NewGuid()}?", "Đáp án chuẩn", 2, lopIds));
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<OralQuestionResponse>>())!.Data!;
    }

    private async Task<List<OralQuestionPracticeResponse>> GetPracticeAsync(string token)
    {
        var request = WithAuth(HttpMethod.Get, "/api/v1/quiz/oral-questions/practice", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<List<OralQuestionPracticeResponse>>>())!.Data!;
    }

    [Fact]
    public async Task Global_oral_question_still_visible_to_every_student_regardless_of_lop()
    {
        var teacherId = Guid.NewGuid();
        var question = await CreateOralQuestionAsync(TestTokens.Teacher(teacherId), "Oral-global-chapter");
        Assert.Empty(question.LopIds);

        var studentWithLop = Guid.NewGuid();
        var studentNoLop = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentWithLop] = Guid.NewGuid();

        var listA = await GetPracticeAsync(TestTokens.Student(studentWithLop));
        var listB = await GetPracticeAsync(TestTokens.Student(studentNoLop));

        Assert.Contains(listA, q => q.Id == question.Id);
        Assert.Contains(listB, q => q.Id == question.Id);
    }

    // Trọng tâm test theo yêu cầu: câu Vấn đáp giao Lớp B -> học viên Lớp A KHÔNG thấy khi lấy pool
    // câu hỏi (dùng chung cho cả luyện tập vấn đáp lẫn thi vấn đáp — getOralQuestions() gọi endpoint
    // /practice này, xem student/thi-thu.html).
    [Fact]
    public async Task Oral_question_scoped_to_one_lop_only_visible_to_students_in_that_lop()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];

        var question = await CreateOralQuestionAsync(TestTokens.Teacher(teacherId), "Oral-scoped-chapter", [lopA]);
        Assert.Equal([lopA], question.LopIds);

        var studentInLopA = Guid.NewGuid();
        var studentInLopB = Guid.NewGuid();
        var studentNoLop = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        _factory.AuthQuizClient.LopIdByUser[studentInLopB] = lopB;

        var listInLopA = await GetPracticeAsync(TestTokens.Student(studentInLopA));
        var listInLopB = await GetPracticeAsync(TestTokens.Student(studentInLopB));
        var listNoLop = await GetPracticeAsync(TestTokens.Student(studentNoLop));

        Assert.Contains(listInLopA, q => q.Id == question.Id);
        Assert.DoesNotContain(listInLopB, q => q.Id == question.Id);
        Assert.DoesNotContain(listNoLop, q => q.Id == question.Id);
    }

    // Trọng tâm test theo yêu cầu: GV không gán được câu Vấn đáp cho lớp không phải mình (403).
    [Fact]
    public async Task Teacher_cannot_scope_oral_question_to_a_lop_they_do_not_own()
    {
        var teacherId = Guid.NewGuid();
        var ownedLop = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [ownedLop];

        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral-questions", TestTokens.Teacher(teacherId));
        request.Content = JsonContent.Create(new CreateOralQuestionRequest("Oral-forbidden-chapter", $"Q {Guid.NewGuid()}?", "Đáp án", 2, [otherLop]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_scope_oral_question_to_any_lop()
    {
        var anyLop = Guid.NewGuid();
        var question = await CreateOralQuestionAsync(TestTokens.Admin(), "Oral-admin-scope-chapter", [anyLop]);
        Assert.Equal([anyLop], question.LopIds);
    }

    [Fact]
    public async Task Teacher_edits_visibility_of_an_existing_global_oral_question_to_their_own_lop()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];

        var question = await CreateOralQuestionAsync(TestTokens.Teacher(teacherId), "Oral-retro-edit-chapter");
        Assert.Empty(question.LopIds);

        var editRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/oral-questions/{question.Id}/lop-visibility", TestTokens.Teacher(teacherId));
        editRequest.Content = JsonContent.Create(new UpdateOralQuestionLopVisibilityRequest([lopA]));
        var editResponse = await _client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();
        var edited = (await editResponse.Content.ReadFromJsonAsync<ApiResponse<OralQuestionResponse>>())!.Data!;
        Assert.Equal([lopA], edited.LopIds);

        var studentInLopA = Guid.NewGuid();
        var studentInLopB = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        _factory.AuthQuizClient.LopIdByUser[studentInLopB] = lopB;

        var listInLopA = await GetPracticeAsync(TestTokens.Student(studentInLopA));
        var listInLopB = await GetPracticeAsync(TestTokens.Student(studentInLopB));
        Assert.Contains(listInLopA, q => q.Id == question.Id);
        Assert.DoesNotContain(listInLopB, q => q.Id == question.Id);
    }

    [Fact]
    public async Task Retroactive_edit_is_also_forbidden_for_an_oral_lop_the_teacher_does_not_own()
    {
        var teacherId = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [];

        var question = await CreateOralQuestionAsync(TestTokens.Teacher(teacherId), "Oral-retro-forbidden-chapter");

        var editRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/oral-questions/{question.Id}/lop-visibility", TestTokens.Teacher(teacherId));
        editRequest.Content = JsonContent.Create(new UpdateOralQuestionLopVisibilityRequest([otherLop]));
        var editResponse = await _client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.Forbidden, editResponse.StatusCode);
    }
}
