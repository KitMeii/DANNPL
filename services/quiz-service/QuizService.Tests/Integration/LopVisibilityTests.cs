using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuizService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace QuizService.Tests.Integration;

/// <summary>Việc 8 (2026-08-16) — giới hạn phạm vi hiển thị câu hỏi/bộ đề theo Lớp cụ thể. Bao phủ:
/// (1) câu hỏi không gán Lớp vẫn toàn hệ thống như cũ (tương thích ngược), (2) câu hỏi giới hạn 1
/// Lớp chỉ hiện đúng học viên Lớp đó, học viên Lớp khác/chưa gán Lớp không thấy, (3) giáo viên chỉ
/// được gán phạm vi tới Lớp mình chủ nhiệm (403 nếu không), Admin gán tự do, (4) sửa lại phạm vi
/// câu hỏi đã có (retroactive edit), (5) publish 1 mã đề giới hạn Lớp lan truyền đúng phạm vi xuống
/// các câu hỏi thành viên đang toàn hệ thống, nhưng không ghi đè câu đã có phạm vi riêng.</summary>
public sealed class LopVisibilityTests : IClassFixture<QuizApiFactory>
{
    private readonly QuizApiFactory _factory;
    private readonly HttpClient _client;

    public LopVisibilityTests(QuizApiFactory factory)
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

    private async Task<QuestionResponse> CreateQuestionAsync(string token, string chapter, List<Guid>? lopIds = null, string sourceType = "Manual")
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", token);
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            chapter, $"Q {Guid.NewGuid()}?", "A", "B", "C", "D", 0, null, SourceType: sourceType, LopIds: lopIds));
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
    }

    private async Task<List<QuizQuestionResponse>> GetPracticeAsync(string token)
    {
        var request = WithAuth(HttpMethod.Get, "/api/v1/quiz/questions/practice", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<List<QuizQuestionResponse>>>())!.Data!;
    }

    [Fact]
    public async Task Global_question_still_visible_to_every_student_regardless_of_lop()
    {
        var teacherId = Guid.NewGuid();
        var teacherToken = TestTokens.Teacher(teacherId);
        var question = await CreateQuestionAsync(teacherToken, "Global-chapter");

        Assert.Empty(question.LopIds);

        var studentWithLopId = Guid.NewGuid();
        var studentNoLopId = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentWithLopId] = Guid.NewGuid();

        var listA = await GetPracticeAsync(TestTokens.Student(studentWithLopId));
        var listB = await GetPracticeAsync(TestTokens.Student(studentNoLopId));

        Assert.Contains(listA, q => q.Id == question.Id);
        Assert.Contains(listB, q => q.Id == question.Id);
    }

    [Fact]
    public async Task Question_scoped_to_one_lop_only_visible_to_students_in_that_lop()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];

        var question = await CreateQuestionAsync(TestTokens.Teacher(teacherId), "Scoped-chapter", [lopA]);
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

    [Fact]
    public async Task Teacher_cannot_scope_question_to_a_lop_they_do_not_own()
    {
        var teacherId = Guid.NewGuid();
        var ownedLop = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [ownedLop];

        var request = WithAuth(HttpMethod.Post, "/api/v1/quiz/questions", TestTokens.Teacher(teacherId));
        request.Content = JsonContent.Create(new CreateQuestionRequest(
            "Forbidden-chapter", $"Q {Guid.NewGuid()}?", "A", "B", "C", "D", 0, null, LopIds: [otherLop]));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_scope_question_to_any_lop()
    {
        var anyLop = Guid.NewGuid();
        var question = await CreateQuestionAsync(TestTokens.Admin(), "Admin-scope-chapter", [anyLop]);

        Assert.Equal([anyLop], question.LopIds);
    }

    [Fact]
    public async Task Teacher_edits_visibility_of_an_existing_global_question_to_their_own_lop()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];

        // Câu hỏi tạo TRƯỚC, không gán Lớp nào — mô phỏng đúng dữ liệu tạo trước Việc 8.
        var question = await CreateQuestionAsync(TestTokens.Teacher(teacherId), "Retro-edit-chapter");
        Assert.Empty(question.LopIds);

        var editRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{question.Id}/lop-visibility", TestTokens.Teacher(teacherId));
        editRequest.Content = JsonContent.Create(new UpdateQuestionLopVisibilityRequest([lopA]));
        var editResponse = await _client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();
        var edited = (await editResponse.Content.ReadFromJsonAsync<ApiResponse<QuestionResponse>>())!.Data!;
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
    public async Task Retroactive_edit_is_also_forbidden_for_a_lop_the_teacher_does_not_own()
    {
        var teacherId = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [];

        var question = await CreateQuestionAsync(TestTokens.Teacher(teacherId), "Retro-forbidden-chapter");

        var editRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/questions/{question.Id}/lop-visibility", TestTokens.Teacher(teacherId));
        editRequest.Content = JsonContent.Create(new UpdateQuestionLopVisibilityRequest([otherLop]));
        var editResponse = await _client.SendAsync(editRequest);

        Assert.Equal(HttpStatusCode.Forbidden, editResponse.StatusCode);
    }

    [Fact]
    public async Task Essay_question_scoped_to_lop_filters_practice_list_the_same_way()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/essay-questions", TestTokens.Teacher(teacherId));
        createRequest.Content = JsonContent.Create(new CreateEssayQuestionRequest("Essay-scope-chapter", $"Q {Guid.NewGuid()}?", "Gợi ý", LopIds: [lopA]));
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var essay = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<EssayQuestionResponse>>())!.Data!;
        Assert.Equal([lopA], essay.LopIds);

        var studentInLopA = Guid.NewGuid();
        var studentInLopB = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        _factory.AuthQuizClient.LopIdByUser[studentInLopB] = lopB;

        var listA = (await (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/essay-questions/practice", TestTokens.Student(studentInLopA))))
            .Content.ReadFromJsonAsync<ApiResponse<List<EssayQuestionPracticeResponse>>>())!.Data!;
        var listB = (await (await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/quiz/essay-questions/practice", TestTokens.Student(studentInLopB))))
            .Content.ReadFromJsonAsync<ApiResponse<List<EssayQuestionPracticeResponse>>>())!.Data!;

        Assert.Contains(listA, q => q.Id == essay.Id);
        Assert.DoesNotContain(listB, q => q.Id == essay.Id);
    }

    [Fact]
    public async Task Publishing_a_lop_scoped_exam_version_propagates_scope_to_previously_global_questions()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopB = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA];
        var teacherToken = TestTokens.Teacher(teacherId);

        // Pool "Imported" — chưa auto-publish, y hệt luồng C2 thật (AI sinh/nhập câu chưa qua kiểm
        // duyệt), và KHÔNG gán Lớp lúc tạo — vẫn toàn hệ thống cho tới khi publish mã đề.
        var poolIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var q = await CreateQuestionAsync(teacherToken, "Propagate-chapter", sourceType: "Imported");
            poolIds.Add(q.Id);
        }

        var generateRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherToken);
        generateRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Propagate test", poolIds, null, 3, 2, [lopA]));
        var generateResponse = await _client.SendAsync(generateRequest);
        generateResponse.EnsureSuccessStatusCode();
        var examSet = (await generateResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        var version = examSet.Versions[0];
        Assert.Equal([lopA], version.LopIds);
        // Chưa publish — câu hỏi thành viên vẫn chưa bị đổi phạm vi.
        Assert.All(version.Questions, q => Assert.Empty(q.LopIds));

        var publishRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{version.Id}/publish", teacherToken);
        var publishResponse = await _client.SendAsync(publishRequest);
        publishResponse.EnsureSuccessStatusCode();

        var studentInLopA = Guid.NewGuid();
        var studentInLopB = Guid.NewGuid();
        _factory.AuthQuizClient.LopIdByUser[studentInLopA] = lopA;
        _factory.AuthQuizClient.LopIdByUser[studentInLopB] = lopB;

        var listA = await GetPracticeAsync(TestTokens.Student(studentInLopA));
        var listB = await GetPracticeAsync(TestTokens.Student(studentInLopB));

        foreach (var q in version.Questions)
        {
            Assert.Contains(listA, x => x.Id == q.Id);
            Assert.DoesNotContain(listB, x => x.Id == q.Id);
        }
    }

    [Fact]
    public async Task Publishing_a_lop_scoped_exam_version_does_not_override_a_question_s_own_existing_scope()
    {
        var teacherId = Guid.NewGuid();
        var lopA = Guid.NewGuid();
        var lopC = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [lopA, lopC];
        var teacherToken = TestTokens.Teacher(teacherId);

        // 1 câu ĐÃ có phạm vi riêng (Lớp C) từ trước, độc lập với mã đề sắp tạo.
        var individuallyScoped = await CreateQuestionAsync(teacherToken, "Override-guard-chapter", [lopC], sourceType: "Imported");
        var plainQuestion = await CreateQuestionAsync(teacherToken, "Override-guard-chapter", sourceType: "Imported");

        var generateRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherToken);
        generateRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest(
            "Override guard test", [individuallyScoped.Id, plainQuestion.Id], null, 2, 2, [lopA]));
        var generateResponse = await _client.SendAsync(generateRequest);
        generateResponse.EnsureSuccessStatusCode();
        var examSet = (await generateResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;

        foreach (var version in examSet.Versions)
        {
            var publishRequest = WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{version.Id}/publish", teacherToken);
            (await _client.SendAsync(publishRequest)).EnsureSuccessStatusCode();
        }

        var getRequest = WithAuth(HttpMethod.Get, "/api/v1/quiz/questions", teacherToken);
        var listResponse = await _client.SendAsync(getRequest);
        var allQuestions = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<QuestionResponse>>>())!.Data!;

        var refreshedScoped = allQuestions.Single(q => q.Id == individuallyScoped.Id);
        var refreshedPlain = allQuestions.Single(q => q.Id == plainQuestion.Id);

        // Câu đã có phạm vi riêng (Lớp C) giữ nguyên — mã đề Lớp A không ghi đè.
        Assert.Equal([lopC], refreshedScoped.LopIds);
        // Câu đang toàn hệ thống thì nhận đúng phạm vi của mã đề (Lớp A).
        Assert.Equal([lopA], refreshedPlain.LopIds);
    }

    [Fact]
    public async Task Exam_version_lop_visibility_endpoint_enforces_teacher_ownership()
    {
        var teacherId = Guid.NewGuid();
        var ownedLop = Guid.NewGuid();
        var otherLop = Guid.NewGuid();
        _factory.AuthQuizClient.OwnedLopIdsByUser[teacherId] = [ownedLop];
        var teacherToken = TestTokens.Teacher(teacherId);

        var question = await CreateQuestionAsync(teacherToken, "Version-visibility-chapter", sourceType: "Imported");
        var generateRequest = WithAuth(HttpMethod.Post, "/api/v1/quiz/exam-sets/generate", teacherToken);
        generateRequest.Content = JsonContent.Create(new GenerateExamSetVersionsRequest("Version visibility test", [question.Id], null, 1, 2));
        var generateResponse = await _client.SendAsync(generateRequest);
        generateResponse.EnsureSuccessStatusCode();
        var examSet = (await generateResponse.Content.ReadFromJsonAsync<ApiResponse<ExamSetResponse>>())!.Data!;
        var versionId = examSet.Versions[0].Id;

        var forbiddenEdit = WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/lop-visibility", teacherToken);
        forbiddenEdit.Content = JsonContent.Create(new UpdateExamVersionLopVisibilityRequest([otherLop]));
        var forbiddenResponse = await _client.SendAsync(forbiddenEdit);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var allowedEdit = WithAuth(HttpMethod.Put, $"/api/v1/quiz/exam-sets/versions/{versionId}/lop-visibility", teacherToken);
        allowedEdit.Content = JsonContent.Create(new UpdateExamVersionLopVisibilityRequest([ownedLop]));
        var allowedResponse = await _client.SendAsync(allowedEdit);
        allowedResponse.EnsureSuccessStatusCode();
        var updated = (await allowedResponse.Content.ReadFromJsonAsync<ApiResponse<ExamVersionResponse>>())!.Data!;
        Assert.Equal([ownedLop], updated.LopIds);
    }
}
