using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Việc 4.4 Phần B (2026-08-20) — "Đề luyện tập" giáo viên tạo. Trọng tâm: (1) giới hạn
/// đúng theo Lớp (như thi thử), (2) LopScopeGuard chặn GV giao lớp không phải mình, (3) form bắt
/// buộc ≥1 Lớp, (4) "đề sống" — câu mới publish vào chương tự động xuất hiện không cần thao tác lại,
/// (5) chỉ người tạo/Admin xóa được.</summary>
public sealed class PracticeSetTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public PracticeSetTests(QuizApiFactory factory)
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

    private async Task<QuestionResponse> CreatePublishedQuestionAsync(string token, string chapter)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", token);
        // SourceType=Manual tự động IsPublishedForPractice=true (xem QuestionService.CreateAsync).
        request.Content = JsonContent.Create(new CreateQuestionRequest(chapter, $"Q {Guid.NewGuid()}?", "A", "B", "C", "D", 0, null));
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
    }

    private async Task<PracticeSetResponse> CreatePracticeSetAsync(string token, string chapter, List<Guid> lopIds, string ten = "Đề luyện tập test")
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/practice-sets", token);
        request.Content = JsonContent.Create(new CreatePracticeSetRequest(ten, chapter, lopIds));
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<PracticeSetResponse>>())!.Data!;
    }

    private async Task<List<PracticeSetResponse>> GetAvailableAsync(string token)
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/practice-sets/available", token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<List<PracticeSetResponse>>>())!.Data!;
    }

    [Fact]
    public async Task Student_in_target_lop_sees_practice_set_others_do_not()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];
        var teacherToken = TestTokens.Teacher(teacherId);

        var chapter = $"Chapter-{Guid.NewGuid()}";
        await CreatePublishedQuestionAsync(teacherToken, chapter);
        var set = await CreatePracticeSetAsync(teacherToken, chapter, [lopA]);
        Assert.Equal([lopA], set.LopIds);
        Assert.Equal(1, set.QuestionCount);

        var studentInLopA = Guid.NewGuid();
        var studentInLopB = Guid.NewGuid();
        var studentNoLop = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        _factory.AuthQuizClient.LopIdByUser[studentInLopB] = lopB;

        var availableA = await GetAvailableAsync(TestTokens.Student(studentInLopA));
        var availableB = await GetAvailableAsync(TestTokens.Student(studentInLopB));
        var availableNoLop = await GetAvailableAsync(TestTokens.Student(studentNoLop));

        Assert.Contains(availableA, s => s.Id == set.Id);
        Assert.DoesNotContain(availableB, s => s.Id == set.Id);
        Assert.DoesNotContain(availableNoLop, s => s.Id == set.Id);
    }

    [Fact]
    public async Task Teacher_cannot_create_practice_set_for_a_lop_they_do_not_own()
    {
        var teacherId = Guid.NewGuid();
        var ownedLop = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [ownedLop];

        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/practice-sets", TestTokens.Teacher(teacherId));
        request.Content = JsonContent.Create(new CreatePracticeSetRequest("Đề bị chặn", "Chapter-X", [otherLop]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creating_practice_set_without_any_lop_is_rejected_by_validation()
    {
        var teacherId = Guid.NewGuid();
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/practice-sets", TestTokens.Teacher(teacherId));
        request.Content = JsonContent.Create(new CreatePracticeSetRequest("Đề không lớp", "Chapter-X", []));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Trọng tâm test theo yêu cầu: publish thêm 1 câu vào chương đã giao -> câu đó tự xuất hiện
    // (đếm tăng) mà GV không cần thao tác lại đề luyện tập.
    [Fact]
    public async Task Practice_set_question_count_reflects_newly_published_questions_without_recreating_it()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];
        var teacherToken = TestTokens.Teacher(teacherId);

        var chapter = $"Chapter-Live-{Guid.NewGuid()}";
        await CreatePublishedQuestionAsync(teacherToken, chapter);
        var set = await CreatePracticeSetAsync(teacherToken, chapter, [lopA]);
        Assert.Equal(1, set.QuestionCount);

        var studentInLopA = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        var studentToken = TestTokens.Student(studentInLopA);

        var beforeAvailable = await GetAvailableAsync(studentToken);
        Assert.Equal(1, beforeAvailable.Single(s => s.Id == set.Id).QuestionCount);

        // GV publish thêm 1 câu vào ĐÚNG chương đó — KHÔNG đụng gì tới PracticeSet.
        await CreatePublishedQuestionAsync(teacherToken, chapter);

        var afterAvailable = await GetAvailableAsync(studentToken);
        Assert.Equal(2, afterAvailable.Single(s => s.Id == set.Id).QuestionCount);
    }

    [Fact]
    public async Task Only_the_creating_teacher_or_admin_can_delete_a_practice_set()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];
        _factory.AuthQuizClient.OwnedLopIdsByUser[otherTeacherId] = [lopA];

        var chapter = $"Chapter-Del-{Guid.NewGuid()}";
        var set = await CreatePracticeSetAsync(TestTokens.Teacher(teacherId), chapter, [lopA]);

        var forbiddenDelete = await _client.SendAsync(WithAuth(HttpMethod.Delete, $"/api/v1/quiz/practice-sets/{set.Id}", TestTokens.Teacher(otherTeacherId)));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        var allowedDelete = await _client.SendAsync(WithAuth(HttpMethod.Delete, $"/api/v1/quiz/practice-sets/{set.Id}", TestTokens.Teacher(teacherId)));
        allowedDelete.EnsureSuccessStatusCode();

        var mineResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/practice-sets/mine", TestTokens.Teacher(teacherId)));
        var mine = (await mineResponse.Content.ReadFromJsonAsync<ApiResponse<List<PracticeSetResponse>>>())!.Data!;
        Assert.DoesNotContain(mine, s => s.Id == set.Id);
    }

    [Fact]
    public async Task Teacher_sees_only_their_own_practice_sets_in_mine_admin_sees_all()
    {
        var teacherA = Guid.NewGuid();
        var teacherB = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherA] = [lopA];
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherB] = [lopB];

        var setA = await CreatePracticeSetAsync(TestTokens.Teacher(teacherA), $"Chapter-Mine-A-{Guid.NewGuid()}", [lopA]);
        var setB = await CreatePracticeSetAsync(TestTokens.Teacher(teacherB), $"Chapter-Mine-B-{Guid.NewGuid()}", [lopB]);

        var mineAResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/practice-sets/mine", TestTokens.Teacher(teacherA)));
        var mineA = (await mineAResponse.Content.ReadFromJsonAsync<ApiResponse<List<PracticeSetResponse>>>())!.Data!;
        Assert.Contains(mineA, s => s.Id == setA.Id);
        Assert.DoesNotContain(mineA, s => s.Id == setB.Id);

        var adminResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/practice-sets/mine", TestTokens.Admin()));
        var adminList = (await adminResponse.Content.ReadFromJsonAsync<ApiResponse<List<PracticeSetResponse>>>())!.Data!;
        Assert.Contains(adminList, s => s.Id == setA.Id);
        Assert.Contains(adminList, s => s.Id == setB.Id);
    }
}
