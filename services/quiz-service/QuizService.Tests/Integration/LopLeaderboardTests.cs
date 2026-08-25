using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QuizService.Api.Clients;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Việc C (2026-08-16) — bảng xếp hạng theo Lớp. Bao phủ: Student chỉ xem đúng lớp mình
/// (403 nếu khác), Teacher chỉ xem đúng lớp mình phụ trách (403 nếu khác), Admin không giới hạn,
/// và học viên chưa từng Thi thử hiện AvgExamScore=null (không phải 0 — khác "Yếu").</summary>
public sealed class LopLeaderboardTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public LopLeaderboardTests(QuizApiFactory factory)
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

    private async Task SeedExamScoreAsync(Guid userId, decimal score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
        db.ExamResults.Add(new ExamResult { UserId = userId, Score = score, Correct = 8, Total = 10, TimeSpentSeconds = 600 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Student_sees_own_lop_leaderboard_with_scores_and_null_for_no_exam_attempts()
    {
        var lopId = Guid.NewGuid();
        var studentWithScore = Guid.NewGuid();
        var studentWithoutScore = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentWithScore] = lopId;
        _factory.AuthQuizClient.RosterByLop[lopId] =
        [
            new RemoteHocVien(studentWithScore, "Học viên A", "Lớp trưởng", "Trung sĩ", "https://example.com/a.jpg"),
            new RemoteHocVien(studentWithoutScore, "Học viên B", "Học viên", null, null),
        ];
        await SeedExamScoreAsync(studentWithScore, 8.5m);

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={lopId}", TestTokens.Student(studentWithScore));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopLeaderboardResponse>>())!.Data!;
        Assert.Equal(2, body.Members.Count);

        var withScore = body.Members.Single(m => m.UserId == studentWithScore);
        Assert.Equal(8.5m, withScore.AvgExamScore);
        Assert.Equal(1, withScore.ExamAttempts);
        // Việc IV (2026-08-20) — ChucVu phải đi xuyên suốt từ roster (auth-service) tới response.
        Assert.Equal("Lớp trưởng", withScore.ChucVu);
        // Rà soát Lần III (2026-08-21, mục C) — CapBac cũng phải đi xuyên suốt, cùng pattern ChucVu.
        Assert.Equal("Trung sĩ", withScore.CapBac);
        // Rà soát Lần V (2026-08-21) — AvatarUrl cũng phải đi xuyên suốt, cùng pattern trên.
        Assert.Equal("https://example.com/a.jpg", withScore.AvatarUrl);

        var withoutScore = body.Members.Single(m => m.UserId == studentWithoutScore);
        Assert.Null(withoutScore.AvgExamScore);
        Assert.Equal(0, withoutScore.ExamAttempts);
        Assert.Null(withoutScore.CapBac);
        Assert.Null(withoutScore.AvatarUrl);
    }

    [Fact]
    public async Task Student_cannot_view_a_different_lop_leaderboard()
    {
        var myLopId = Guid.NewGuid();
        var otherLopId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentId] = myLopId;

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={otherLopId}", TestTokens.Student(studentId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Student_with_no_lop_assigned_cannot_view_any_lop_leaderboard()
    {
        var someLopId = Guid.NewGuid();
        var studentId = Guid.NewGuid(); // không seed LopIdByUser — mô phỏng học viên chưa gán Lớp

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={someLopId}", TestTokens.Student(studentId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_sees_leaderboard_of_a_lop_they_own()
    {
        var lopId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopId];
        _factory.AuthQuizClient.RosterByLop[lopId] = [new RemoteHocVien(studentId, "Học viên C", "Học viên", null, null)];

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={lopId}", TestTokens.Teacher(teacherId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopLeaderboardResponse>>())!.Data!;
        Assert.Single(body.Members);
    }

    [Fact]
    public async Task Teacher_cannot_view_a_lop_they_do_not_own()
    {
        var ownedLop = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [ownedLop];

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={otherLop}", TestTokens.Teacher(teacherId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_view_any_lop_leaderboard_without_ownership()
    {
        var lopId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _factory.AuthQuizClient.RosterByLop[lopId] = [new RemoteHocVien(studentId, "Học viên D", "Học viên", null, null)];

        var request = WithAuth(HttpMethod.Get, $"/api/v1/quiz/stats/leaderboard-by-lop?lopId={lopId}", TestTokens.Admin());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await _client.GetAsync($"/api/v1/quiz/stats/leaderboard-by-lop?lopId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
