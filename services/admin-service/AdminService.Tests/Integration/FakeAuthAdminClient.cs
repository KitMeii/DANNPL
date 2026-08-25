using AdminService.Api.Clients;
using Shared.Infrastructure.Common;

namespace AdminService.Tests.Integration;

/// <summary>Stands in for auth-service (list users / change role / khóa / lớp) so tests don't need
/// a live instance. Seed <see cref="Users"/>/<see cref="Khoas"/>/<see cref="Lops"/> before each
/// test — set RemoteUser's LopId directly (record `with` expression) to simulate lớp membership.</summary>
public sealed class FakeAuthAdminClient : IAuthAdminClient
{
    public List<RemoteUser> Users { get; } = [];
    public Dictionary<Guid, RemoteKhoa> Khoas { get; } = [];
    public Dictionary<Guid, RemoteLop> Lops { get; } = [];
    public List<RemoteLopActivityLog> ActivityLogs { get; } = [];

    /// <summary>Việc 4.2 mục 3 test hook — ném lỗi khi != null, mô phỏng auth-service bị gián đoạn
    /// giữa saga (kiểm tra saga dừng đúng bước, báo cáo đúng lỗi, và có thể gọi lại an toàn).</summary>
    public Exception? DeleteAllLopDataFailure { get; set; }
    public int DeleteAllLopDataCallCount { get; private set; }

    public Task<IReadOnlyList<RemoteUser>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct)
    {
        IEnumerable<RemoteUser> query = Users;

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (lopId is not null)
        {
            query = query.Where(u => u.LopId == lopId);
        }

        if (khoaId is not null)
        {
            var lopIdsInKhoa = Lops.Values.Where(l => l.KhoaId == khoaId).Select(l => l.Id).ToHashSet();
            query = query.Where(u => u.LopId is not null && lopIdsInKhoa.Contains(u.LopId.Value));
        }

        IReadOnlyList<RemoteUser> result = query.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RemoteUser>> ListHocVienAsync(Guid lopId, CancellationToken ct)
    {
        IReadOnlyList<RemoteUser> result = Users.Where(u => u.LopId == lopId && u.Role == Shared.Contracts.Roles.Student).ToList();
        return Task.FromResult(result);
    }

    public Task<RemoteUser> ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct)
    {
        var index = Users.FindIndex(u => u.Id == userId);
        if (index < 0)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        var updated = Users[index] with { Role = newRole };
        Users[index] = updated;
        return Task.FromResult(updated);
    }

    public Task<RemoteUser> CreateUserAsync(string email, string password, string name, string role, CancellationToken ct)
    {
        if (Users.Any(u => u.Email == email))
        {
            throw new ConflictException("Email đã được đăng ký.");
        }

        var created = new RemoteUser(Guid.NewGuid(), email, name, role);
        Users.Add(created);
        return Task.FromResult(created);
    }

    public Task<RemoteUser> SetUserLockedAsync(Guid userId, bool isLocked, CancellationToken ct)
    {
        var index = Users.FindIndex(u => u.Id == userId);
        if (index < 0)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        var updated = Users[index] with { IsLocked = isLocked };
        Users[index] = updated;
        return Task.FromResult(updated);
    }

    public Task<RemoteKhoa> GetKhoaAsync(Guid khoaId, CancellationToken ct)
    {
        if (!Khoas.TryGetValue(khoaId, out var khoa))
        {
            throw new NotFoundException("Không tìm thấy khóa.");
        }

        return Task.FromResult(khoa);
    }

    public Task<RemoteLop> GetLopAsync(Guid lopId, CancellationToken ct)
    {
        if (!Lops.TryGetValue(lopId, out var lop))
        {
            throw new NotFoundException("Không tìm thấy lớp.");
        }

        return Task.FromResult(lop);
    }

    public Task<IReadOnlyList<RemoteLop>> ListLopAsync(CancellationToken ct)
    {
        IReadOnlyList<RemoteLop> result = Lops.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RemoteLopActivityLog>> ListAllLopActivityLogAsync(Guid lopId, CancellationToken ct)
    {
        IReadOnlyList<RemoteLopActivityLog> result = ActivityLogs.Where(l => l.LopId == lopId).ToList();
        return Task.FromResult(result);
    }

    public Task<RemoteDeleteAllLopDataResult> DeleteAllLopDataAsync(Guid lopId, CancellationToken ct)
    {
        DeleteAllLopDataCallCount++;
        if (DeleteAllLopDataFailure is not null)
        {
            throw DeleteAllLopDataFailure;
        }

        if (!Lops.Remove(lopId, out _))
        {
            return Task.FromResult(new RemoteDeleteAllLopDataResult(0, 0, false));
        }

        var students = Users.Where(u => u.LopId == lopId && u.Role == Shared.Contracts.Roles.Student).ToList();
        foreach (var s in students)
        {
            Users.Remove(s);
        }

        var logsRemoved = ActivityLogs.RemoveAll(l => l.LopId == lopId);

        return Task.FromResult(new RemoteDeleteAllLopDataResult(students.Count, logsRemoved, true));
    }
}
