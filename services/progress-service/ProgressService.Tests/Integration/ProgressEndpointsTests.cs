using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProgressService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace ProgressService.Tests.Integration;

public sealed class ProgressEndpointsTests : IClassFixture<ProgressApiFactory>
{
    private readonly ProgressApiFactory _factory;
    private readonly HttpClient _client;

    public ProgressEndpointsTests(ProgressApiFactory factory)
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

    // POST /record-score additionally requires X-Internal-Key — simulates the one legitimate
    // caller (quiz-service's HttpProgressReporter). See RequireInternalServiceKeyFilter.
    private static HttpRequestMessage WithAuthAndInternalKey(HttpMethod method, string url, string token)
    {
        var request = WithAuth(method, url, token);
        request.Headers.Add("X-Internal-Key", ProgressApiFactory.TestInternalServiceKey);
        return request;
    }

    [Fact]
    public async Task Logging_study_time_twice_same_day_accumulates_minutes_and_keeps_streak_at_one()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Student(userId);

        var first = WithAuth(HttpMethod.Post, "/api/v1/progress/study-logs", token);
        first.Content = JsonContent.Create(new LogStudyTimeRequest(20));
        await _client.SendAsync(first);

        var second = WithAuth(HttpMethod.Post, "/api/v1/progress/study-logs", token);
        second.Content = JsonContent.Create(new LogStudyTimeRequest(15));
        await _client.SendAsync(second);

        var meRequest = WithAuth(HttpMethod.Get, "/api/v1/progress/me", token);
        var meResponse = await _client.SendAsync(meRequest);
        var me = (await meResponse.Content.ReadFromJsonAsync<ApiResponse<MyProgressResponse>>())!.Data!;

        Assert.Equal(35, me.TotalStudyMinutes);
        Assert.Equal(1, me.Streak);
    }

    [Fact]
    public async Task Weekly_study_logs_include_todays_entry()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Student(userId);

        var logRequest = WithAuth(HttpMethod.Post, "/api/v1/progress/study-logs", token);
        logRequest.Content = JsonContent.Create(new LogStudyTimeRequest(10));
        await _client.SendAsync(logRequest);

        var weeklyRequest = WithAuth(HttpMethod.Get, "/api/v1/progress/study-logs/weekly", token);
        var weeklyResponse = await _client.SendAsync(weeklyRequest);
        var weekly = (await weeklyResponse.Content.ReadFromJsonAsync<ApiResponse<List<StudyLogResponse>>>())!.Data!;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Contains(weekly, d => d.StudyDate == today && d.Minutes == 10);
    }

    [Fact]
    public async Task Record_score_computes_a_running_average_not_the_last_value()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Student(userId);

        foreach (var score in new[] { 10m, 6m, 8m })
        {
            var request = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/progress/record-score", token);
            request.Content = JsonContent.Create(new RecordScoreRequest(score));
            await _client.SendAsync(request);
        }

        var meRequest = WithAuth(HttpMethod.Get, "/api/v1/progress/me", token);
        var meResponse = await _client.SendAsync(meRequest);
        var me = (await meResponse.Content.ReadFromJsonAsync<ApiResponse<MyProgressResponse>>())!.Data!;

        Assert.Equal(3, me.TotalAttempts);
        Assert.Equal(8m, me.AvgScore); // (10+6+8)/3 = 8
    }

    [Fact]
    public async Task Leaderboard_is_sorted_by_average_score_and_enriched_with_names()
    {
        var topUser = Guid.NewGuid();
        var lowUser = Guid.NewGuid();
        _factory.NameLookup.Add(topUser, "Học viên Giỏi");
        _factory.NameLookup.Add(lowUser, "Học viên Khá");

        var topRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/progress/record-score", TestTokens.Student(topUser));
        topRequest.Content = JsonContent.Create(new RecordScoreRequest(10m));
        await _client.SendAsync(topRequest);

        var lowRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/progress/record-score", TestTokens.Student(lowUser));
        lowRequest.Content = JsonContent.Create(new RecordScoreRequest(5m));
        await _client.SendAsync(lowRequest);

        var leaderboardRequest = WithAuth(HttpMethod.Get, "/api/v1/progress/leaderboard", TestTokens.Student(Guid.NewGuid()));
        var leaderboardResponse = await _client.SendAsync(leaderboardRequest);
        var leaderboard = (await leaderboardResponse.Content.ReadFromJsonAsync<ApiResponse<List<LeaderboardEntryResponse>>>())!.Data!;

        var topIndex = leaderboard.FindIndex(e => e.UserId == topUser);
        var lowIndex = leaderboard.FindIndex(e => e.UserId == lowUser);
        Assert.True(topIndex >= 0 && lowIndex >= 0 && topIndex < lowIndex);
        Assert.Equal("Học viên Giỏi", leaderboard[topIndex].Name);
    }

    // Việc B (2026-08-16) — fix bug xác nhận qua ảnh chụp thật: "Giáo viên Demo" lọt vào bảng xếp
    // hạng vì StudentProgress không phân biệt role (Teacher/Admin tự thử làm bài vẫn tạo progress
    // y hệt học viên). Đặt điểm Teacher CAO HƠN Student — nếu bug tái diễn (lọc sau khi Take(top)
    // thay vì trước), Teacher sẽ chiếm vị trí #1 thay vì bị loại hẳn, test này bắt được cả 2 kiểu lỗi.
    [Fact]
    public async Task Leaderboard_excludes_teacher_and_admin_accounts_even_when_they_outscore_students()
    {
        var teacherUser = Guid.NewGuid();
        var studentUser = Guid.NewGuid();
        _factory.NameLookup.Add(teacherUser, "Giáo viên Demo", "Teacher");
        _factory.NameLookup.Add(studentUser, "Học viên Thật", "Student");

        var teacherRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/progress/record-score", TestTokens.Teacher(teacherUser));
        teacherRequest.Content = JsonContent.Create(new RecordScoreRequest(10m));
        await _client.SendAsync(teacherRequest);

        var studentRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/progress/record-score", TestTokens.Student(studentUser));
        studentRequest.Content = JsonContent.Create(new RecordScoreRequest(5m));
        await _client.SendAsync(studentRequest);

        var leaderboardRequest = WithAuth(HttpMethod.Get, "/api/v1/progress/leaderboard", TestTokens.Student(Guid.NewGuid()));
        var leaderboardResponse = await _client.SendAsync(leaderboardRequest);
        var leaderboard = (await leaderboardResponse.Content.ReadFromJsonAsync<ApiResponse<List<LeaderboardEntryResponse>>>())!.Data!;

        Assert.DoesNotContain(leaderboard, e => e.UserId == teacherUser);
        Assert.Contains(leaderboard, e => e.UserId == studentUser);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/progress/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Phần A RBAC audit: a student's own valid JWT must not be enough to record an arbitrary
    // score for themselves — that would let anyone fabricate a leaderboard position without ever
    // answering a quiz through quiz-service (repeat of the F3 client-trusted-scoring finding).
    // Only a caller that also knows InternalService:SharedKey (i.e. quiz-service) may proceed.
    [Fact]
    public async Task Student_without_internal_key_cannot_record_a_fabricated_score()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/progress/record-score", TestTokens.Student(Guid.NewGuid()));
        request.Content = JsonContent.Create(new RecordScoreRequest(10m));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
