using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Việc 4.1 (2026-08-19) — chống thoát thi thử. See ExamSession.cs remarks for the
/// "1 InProgress session per user+kind" invariant these tests exist to prove.</summary>
public sealed class ExamSessionTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public ExamSessionTests(QuizApiFactory factory)
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
        request.Content = JsonContent.Create(new CreateQuestionRequest("1", questionText, "A", "B", "C", "D", correctAnswer, "giải thích"));
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
    }

    private async Task<OralQuestionResponse> CreateOralQuestionAsync(string questionText)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral-questions", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateOralQuestionRequest("1", questionText, "đáp án mẫu", 1));
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<OralQuestionResponse>>())!.Data!;
    }

    private async Task<Guid> StartExamSessionAsync(string token, List<Guid> questionIds, int durationSeconds = 2700)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/exams/start", token);
        request.Content = JsonContent.Create(new StartExamSessionRequest(questionIds, durationSeconds));
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<StartExamSessionResponse>>())!.Data!.SessionId;
    }

    private async Task<List<MyResultResponse>> GetMyResultsAsync(string token)
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/my-results", token));
        return (await response.Content.ReadFromJsonAsync<ApiResponse<List<MyResultResponse>>>())!.Data!;
    }

    [Fact]
    public async Task Auto_submit_on_abandon_grades_against_full_pool_unanswered_counts_wrong()
    {
        var q1 = await CreateQuestionAsync("Q1 (auto-submit)?", correctAnswer: 1);
        var q2 = await CreateQuestionAsync("Q2 (auto-submit, bỏ dở)?", correctAnswer: 2);
        var q3 = await CreateQuestionAsync("Q3 (auto-submit, bỏ dở)?", correctAnswer: 0);
        var token = TestTokens.Student();

        var sessionId = await StartExamSessionAsync(token, [q1.Id, q2.Id, q3.Id]);

        // Chỉ trả lời đúng Q1 rồi "thoát" — beacon gửi những gì đã có trong bộ nhớ lúc đó.
        var autoSubmit = WithAuth(HttpMethod.Post, "/api/v1/quiz/exams/auto-submit", token);
        autoSubmit.Content = JsonContent.Create(new AutoSubmitExamRequest(sessionId, [new SubmitAnswerItem(q1.Id, 1)]));
        var response = await _client.SendAsync(autoSubmit);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await GetMyResultsAsync(token);
        var examResult = Assert.Single(results, r => r.Kind == "exam");

        Assert.True(examResult.IsAutoSubmitted);
        Assert.Equal(1, examResult.Correct);
        // Mẫu số = TOÀN BỘ 3 câu được giao lúc bắt đầu, KHÔNG phải chỉ 1 câu đã trả lời — Q2/Q3
        // chưa làm tính sai, đúng quyết định đã chốt (khác hẳn nộp tay bình thường).
        Assert.Equal(3, examResult.Total);
        Assert.Equal(Math.Round(1 * 10m / 3, 2), examResult.Score);
    }

    [Fact]
    public async Task Starting_a_new_session_finalizes_the_old_InProgress_session_first_no_cherry_picking()
    {
        var q1 = await CreateQuestionAsync("Q1 (phiên cũ bị bỏ)?", correctAnswer: 0);
        var q2 = await CreateQuestionAsync("Q2 (phiên mới)?", correctAnswer: 0);
        var token = TestTokens.Student();

        // Mở phiên 1, KHÔNG nộp / không có beacon nào — mô phỏng học viên mở rồi bỏ đó, định bụng
        // "nếu làm phiên 2 tốt hơn thì chỉ nộp phiên 2".
        var session1 = await StartExamSessionAsync(token, [q1.Id]);

        // Mở phiên 2 — PHẢI tự động chốt phiên 1 trước, không cho giữ song song.
        var session2 = await StartExamSessionAsync(token, [q2.Id]);
        Assert.NotEqual(session1, session2);

        var results = await GetMyResultsAsync(token);
        var examResults = results.Where(r => r.Kind == "exam").ToList();

        // Phiên 1 đã bị chốt (0 điểm, không có gì được nộp) NGAY khi phiên 2 mở — không có cách
        // nào "giữ" phiên 1 chờ xem phiên 2 ra sao rồi mới quyết định.
        var abandoned = Assert.Single(examResults, r => r.IsAutoSubmitted);
        Assert.Equal(0, abandoned.Correct);
        Assert.Equal(1, abandoned.Total);
    }

    [Fact]
    public async Task Beacon_called_twice_for_the_same_session_does_not_create_duplicate_ExamResult()
    {
        var q1 = await CreateQuestionAsync("Q1 (idempotent)?", correctAnswer: 0);
        var token = TestTokens.Student();
        var sessionId = await StartExamSessionAsync(token, [q1.Id]);

        var first = WithAuth(HttpMethod.Post, "/api/v1/quiz/exams/auto-submit", token);
        first.Content = JsonContent.Create(new AutoSubmitExamRequest(sessionId, []));
        await _client.SendAsync(first);

        // Lần 2 mô phỏng lazy-check (hoặc 1 beacon thứ 2 do trình duyệt retry) cố chốt CHÍNH session
        // vừa bị chốt — phải là no-op, không tạo thêm dòng.
        var second = WithAuth(HttpMethod.Post, "/api/v1/quiz/exams/auto-submit", token);
        second.Content = JsonContent.Create(new AutoSubmitExamRequest(sessionId, []));
        var secondResponse = await _client.SendAsync(second);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode); // vẫn 200 (best-effort cho beacon)

        var results = await GetMyResultsAsync(token);
        Assert.Single(results, r => r.Kind == "exam");
    }

    [Fact]
    public async Task Manual_submit_with_session_id_marks_submitted_not_auto_submitted_and_lazy_check_does_not_touch_it_again()
    {
        var q1 = await CreateQuestionAsync("Q1 (nộp tay có session)?", correctAnswer: 1);
        var token = TestTokens.Student();
        var sessionId = await StartExamSessionAsync(token, [q1.Id]);

        var submit = WithAuth(HttpMethod.Post, "/api/v1/quiz/exams/submit", token);
        submit.Content = JsonContent.Create(new SubmitExamRequest([new SubmitAnswerItem(q1.Id, 1)], TimeSpentSeconds: 60, SessionId: sessionId));
        var response = await _client.SendAsync(submit);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await GetMyResultsAsync(token);
        var examResult = Assert.Single(results, r => r.Kind == "exam");
        Assert.False(examResult.IsAutoSubmitted);
        Assert.Equal(10.0m, examResult.Score);

        // Gọi my-results lần 2 (kích hoạt lazy-check) — session đã Submitted, không còn InProgress
        // nên không bị đụng tới, vẫn đúng 1 dòng.
        var resultsAgain = await GetMyResultsAsync(token);
        Assert.Single(resultsAgain, r => r.Kind == "exam");
    }

    [Fact]
    public async Task Lazy_check_finalizes_a_session_past_expected_duration_and_grace_period_with_no_beacon()
    {
        var q1 = await CreateQuestionAsync("Q1 (lazy-check hết giờ)?", correctAnswer: 0);
        var token = TestTokens.Student();
        var sessionId = await StartExamSessionAsync(token, [q1.Id], durationSeconds: 60);

        // "Tua" thời gian bắt đầu về quá khứ đủ xa để vượt thời lượng (60s) + khoảng đệm 2 phút —
        // mô phỏng học viên bỏ dở lâu, KHÔNG có beacon nào tới nơi (crash/mất mạng đột ngột).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
            var session = await db.ExamSessions.SingleAsync(s => s.Id == sessionId);
            session.StartedAtUtc = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var results = await GetMyResultsAsync(token);
        var examResult = Assert.Single(results, r => r.Kind == "exam");

        Assert.True(examResult.IsAutoSubmitted);
        Assert.Equal(0, examResult.Correct);
        Assert.Equal(1, examResult.Total);
    }

    [Fact]
    public async Task Oral_session_marked_submitted_once_every_assigned_question_is_answered()
    {
        var q1 = await CreateOralQuestionAsync("Câu vấn đáp session — hoàn tất?");
        var token = TestTokens.Student();

        var start = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral/start", token);
        start.Content = JsonContent.Create(new StartExamSessionRequest([q1.Id], 180));
        var sessionId = (await (await _client.SendAsync(start)).Content.ReadFromJsonAsync<ApiResponse<StartExamSessionResponse>>())!.Data!.SessionId;

        var submit = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral/submit", token);
        submit.Content = JsonContent.Create(new SubmitOralRequest(q1.Id, "câu trả lời", null, sessionId));
        await _client.SendAsync(submit);

        var sessionsResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/oral/sessions", token));
        var sessions = (await sessionsResponse.Content.ReadFromJsonAsync<ApiResponse<List<OralSessionResponse>>>())!.Data!;
        var session = Assert.Single(sessions, s => s.Id == sessionId);

        Assert.Equal("Submitted", session.Status);
        Assert.Equal(1, session.AnsweredCount);
        Assert.Equal(1, session.QuestionCount);
    }

    [Fact]
    public async Task Oral_session_abandoned_via_beacon_marks_incomplete_session_visibly()
    {
        var q1 = await CreateOralQuestionAsync("Câu 1 vấn đáp — sẽ bỏ dở?");
        var q2 = await CreateOralQuestionAsync("Câu 2 vấn đáp — sẽ bỏ dở?");
        var token = TestTokens.Student();

        var start = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral/start", token);
        start.Content = JsonContent.Create(new StartExamSessionRequest([q1.Id, q2.Id], 360));
        var sessionId = (await (await _client.SendAsync(start)).Content.ReadFromJsonAsync<ApiResponse<StartExamSessionResponse>>())!.Data!.SessionId;

        // Chỉ trả lời câu 1, rồi "thoát" — beacon gọi /oral/abandon.
        var submit = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral/submit", token);
        submit.Content = JsonContent.Create(new SubmitOralRequest(q1.Id, "câu trả lời", null, sessionId));
        await _client.SendAsync(submit);

        var abandon = WithAuth(HttpMethod.Post, "/api/v1/quiz/oral/abandon", token);
        abandon.Content = JsonContent.Create(new AbandonOralSessionRequest(sessionId));
        await _client.SendAsync(abandon);

        var sessionsResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/oral/sessions", token));
        var sessions = (await sessionsResponse.Content.ReadFromJsonAsync<ApiResponse<List<OralSessionResponse>>>())!.Data!;
        var session = Assert.Single(sessions, s => s.Id == sessionId);

        Assert.Equal("AutoSubmittedAbandoned", session.Status);
        Assert.Equal(1, session.AnsweredCount);
        Assert.Equal(2, session.QuestionCount);
    }

    // navigator.sendBeacon() cannot set an Authorization header — this endpoint must accept the
    // JWT via query string as a fallback (see Shared.Infrastructure/Auth/JwtAuthenticationExtensions.cs).
    [Fact]
    public async Task Auto_submit_endpoint_accepts_jwt_via_query_string_for_sendBeacon_compatibility()
    {
        var q1 = await CreateQuestionAsync("Q1 (query-string auth)?", correctAnswer: 1);
        var token = TestTokens.Student();
        var sessionId = await StartExamSessionAsync(token, [q1.Id]);

        // KHÔNG set header Authorization — đúng giới hạn thật của navigator.sendBeacon().
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/quiz/exams/auto-submit?access_token={token}",
            new AutoSubmitExamRequest(sessionId, [new SubmitAnswerItem(q1.Id, 1)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await GetMyResultsAsync(token);
        Assert.Single(results, r => r.Kind == "exam" && r.IsAutoSubmitted);
    }

    [Fact]
    public async Task Start_exam_session_without_auth_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/quiz/exams/start", new StartExamSessionRequest([Guid.NewGuid()], 60));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
