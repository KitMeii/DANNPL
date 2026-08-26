using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

public sealed class StatsEndpointsTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public StatsEndpointsTests(QuizApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuthAndKey(HttpMethod method, string url, string token, bool includeKey = true)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (includeKey)
        {
            request.Headers.Add("X-Internal-Key", QuizApiFactory.TestInternalServiceKey);
        }
        return request;
    }

    private async Task SeedResultsAsync(Guid userId, decimal? examScore1, decimal? examScore2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();

        if (examScore1 is not null)
        {
            db.ExamResults.Add(new ExamResult { UserId = userId, Score = examScore1.Value, Correct = 8, Total = 10, TimeSpentSeconds = 600 });
        }
        if (examScore2 is not null)
        {
            db.ExamResults.Add(new ExamResult { UserId = userId, Score = examScore2.Value, Correct = 6, Total = 10, TimeSpentSeconds = 500 });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_exam_average_per_user()
    {
        var userId = Guid.NewGuid();
        await SeedResultsAsync(userId, examScore1: 8m, examScore2: 6m);

        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Admin());
        request.Content = JsonContent.Create(new ScoresByUsersRequest([userId]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<ScoresByUsersResponse>>())!.Data!;
        var summary = Assert.Single(body.Users);

        Assert.Equal(userId, summary.UserId);
        Assert.Equal(7m, summary.AvgExamScore); // (8 + 6) / 2
        Assert.Equal(2, summary.ExamAttempts);
    }

    [Fact]
    public async Task User_with_no_attempts_returns_null_averages_and_zero_counts()
    {
        var userId = Guid.NewGuid();

        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Admin());
        request.Content = JsonContent.Create(new ScoresByUsersRequest([userId]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<ScoresByUsersResponse>>())!.Data!;
        var summary = Assert.Single(body.Users);

        Assert.Null(summary.AvgExamScore);
        Assert.Equal(0, summary.ExamAttempts);
    }

    [Fact]
    public async Task Missing_internal_key_is_rejected_even_with_valid_admin_jwt()
    {
        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Admin(), includeKey: false);
        request.Content = JsonContent.Create(new ScoresByUsersRequest([Guid.NewGuid()]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Student (không phải Admin/Teacher) vẫn bị chặn dù có đúng X-Internal-Key — endpoint chỉ mở
    // cho Admin/Teacher (Gap 2: Teacher cần gọi qua admin-service để xem điểm lớp mình phụ trách).
    [Fact]
    public async Task Non_admin_non_teacher_role_is_rejected_even_with_valid_internal_key()
    {
        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Student());
        request.Content = JsonContent.Create(new ScoresByUsersRequest([Guid.NewGuid()]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_role_is_accepted_with_valid_internal_key()
    {
        var userId = Guid.NewGuid();
        await SeedResultsAsync(userId, examScore1: 8m, examScore2: null);

        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Teacher());
        request.Content = JsonContent.Create(new ScoresByUsersRequest([userId]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Empty_user_id_list_is_rejected()
    {
        var request = WithAuthAndKey(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users", TestTokens.Admin());
        request.Content = JsonContent.Create(new ScoresByUsersRequest([]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
