namespace ProgressService.Api.Dtos;

public sealed record LogStudyTimeRequest(int Minutes);

public sealed record StudyLogResponse(DateOnly StudyDate, int Minutes);

public sealed record RecordScoreRequest(decimal Score);

public sealed record MyProgressResponse(int Streak, DateOnly? LastStudyDate, int TotalStudyMinutes, int TotalAttempts, decimal AvgScore);

public sealed record LeaderboardEntryResponse(Guid UserId, string Name, decimal AvgScore, int Streak, int TotalAttempts);
