using QuizService.Api.Clients;
using Shared.Contracts;

namespace QuizService.Api.Services;

public sealed class LopScopeGuard(IAuthQuizClient authClient) : ILopScopeGuard
{
    public async Task EnsureCanAssignAsync(IReadOnlyList<Guid> lopIds, string callerRole, CancellationToken ct)
    {
        if (lopIds.Count == 0 || callerRole == Roles.Admin)
        {
            return;
        }

        var myLopIds = (await authClient.ListMyLopIdsAsync(ct)).ToHashSet();
        var notOwned = lopIds.Where(id => !myLopIds.Contains(id)).ToList();
        if (notOwned.Count > 0)
        {
            throw new UnauthorizedAccessException("Bạn chỉ được giới hạn phạm vi hiển thị tới Lớp mình đang phụ trách.");
        }
    }
}
