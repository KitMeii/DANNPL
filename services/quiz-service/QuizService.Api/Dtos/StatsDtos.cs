namespace QuizService.Api.Dtos;

public sealed record ScoresByUsersRequest(IReadOnlyList<Guid> UserIds);

/// <summary>Điểm trung bình/số lượt Kiểm tra (ExamResult). Avg = null nghĩa là chưa có lượt nào,
/// không phải 0 điểm. (Trước có thêm cặp Practice song song — bỏ cùng lúc xóa tính năng Luyện
/// tập.)</summary>
public sealed record UserScoreSummary(
    Guid UserId,
    decimal? AvgExamScore,
    int ExamAttempts);

public sealed record ScoresByUsersResponse(IReadOnlyList<UserScoreSummary> Users);

/// <summary>Việc C (2026-08-16) — 1 dòng bảng xếp hạng theo Lớp. AvgExamScore null = học viên
/// CHƯA từng Kiểm tra (khác 0 điểm) — FE hiện dải màu "Chưa có dữ liệu" riêng, không xếp vào "Yếu".
/// ChucVu thêm ở Việc IV (2026-08-20, rà soát Lần II mục 1.3) để FE hiện badge Chức vụ. CapBac thêm
/// ở Rà soát Lần III (2026-08-21, mục C) — trước đây hoàn toàn chưa nối dây, không phải bug dữ liệu
/// null như ban đầu tưởng. AvatarUrl thêm ở Rà soát Lần V để FE hiện ảnh đại diện thật thay vì chữ
/// cái đầu tên.</summary>
public sealed record LopLeaderboardEntryResponse(
    Guid UserId,
    string Name,
    string ChucVu,
    string? CapBac,
    string? AvatarUrl,
    decimal? AvgExamScore,
    int ExamAttempts);

public sealed record LopLeaderboardResponse(Guid LopId, IReadOnlyList<LopLeaderboardEntryResponse> Members);
