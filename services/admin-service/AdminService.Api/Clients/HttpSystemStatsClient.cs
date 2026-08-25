using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Clients;

public sealed class HttpSystemStatsClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor) : ISystemStatsClient
{
    private sealed record IdOnly(Guid Id);

    // Chỉ khai thêm field cần dùng — System.Text.Json bỏ qua mọi field khác của QuestionResponse/
    // MaterialResponse thật (chapter/text/options/... không cần ở đây), không cần DTO chung giữa
    // 2 service khác database.
    private sealed record QuestionCreatorAndChapter(Guid? CreatedBy, string? Chapter);
    private sealed record MaterialCreator(Guid UploadedBy);

    public async Task<SystemOverview> GetOverviewAsync(CancellationToken ct)
    {
        var materials = await CountAsync("content-service", "/api/v1/content/materials", ct);
        var questions = await CountAsync("quiz-service", "/api/v1/quiz/questions", ct);
        var oralQuestions = await CountAsync("quiz-service", "/api/v1/quiz/oral-questions", ct);

        return new SystemOverview(materials, questions, oralQuestions);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentCounts>> GetContentCountsByCreatorAsync(CancellationToken ct)
    {
        var questions = await FetchListAsync<QuestionCreatorAndChapter>("quiz-service", "/api/v1/quiz/questions", ct);
        var materials = await FetchListAsync<MaterialCreator>("content-service", "/api/v1/content/materials", ct);

        var questionCounts = questions
            .Where(q => q.CreatedBy.HasValue)
            .GroupBy(q => q.CreatedBy!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var materialCounts = materials
            .GroupBy(m => m.UploadedBy)
            .ToDictionary(g => g.Key, g => g.Count());

        var creatorIds = questionCounts.Keys.Union(materialCounts.Keys);
        return creatorIds.ToDictionary(
            id => id,
            id => new ContentCounts(questionCounts.GetValueOrDefault(id), materialCounts.GetValueOrDefault(id)));
    }

    public async Task<IReadOnlyDictionary<string, int>> GetQuestionCountsByChapterAsync(CancellationToken ct)
    {
        var questions = await FetchListAsync<QuestionCreatorAndChapter>("quiz-service", "/api/v1/quiz/questions", ct);
        return questions
            .GroupBy(q => string.IsNullOrWhiteSpace(q.Chapter) ? "Chung" : q.Chapter!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<int> CountAsync(string clientName, string url, CancellationToken ct) =>
        (await FetchListAsync<IdOnly>(clientName, url, ct)).Count;

    private async Task<List<T>> FetchListAsync<T>(string clientName, string url, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(clientName);

        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        var response = await client.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<T>>>(cancellationToken: ct);
        return body?.Data ?? [];
    }
}
