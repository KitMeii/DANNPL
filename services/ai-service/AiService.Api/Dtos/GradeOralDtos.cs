namespace AiService.Api.Dtos;

/// <summary>Field names must stay in sync with quiz-service's OralGradingRequest/OralGradingResult
/// (services/quiz-service/QuizService.Api/Grading/IOralGradingClient.cs) — that's the caller of
/// this endpoint. No shared assembly on purpose: services only talk over HTTP/JSON.</summary>
public sealed record GradeOralRequest(string QuestionText, string? ExpectedAnswer, string MainAnswer, IReadOnlyList<string> FollowupAnswers);

public sealed record GradeOralResponse(decimal Score, string? Comment, IReadOnlyDictionary<string, decimal>? RubricScores);
