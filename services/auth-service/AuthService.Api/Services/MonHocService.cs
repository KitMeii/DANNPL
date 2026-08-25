using AuthService.Api.Data;
using AuthService.Api.Dtos;
using AuthService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace AuthService.Api.Services;

public sealed class MonHocService(AuthDbContext db) : IMonHocService
{
    public async Task<IReadOnlyList<MonHocResponse>> ListAsync(CancellationToken ct)
    {
        var monHocs = await db.MonHocs.OrderByDescending(m => m.CreatedAtUtc).ToListAsync(ct);
        return await ToResponsesAsync(monHocs, ct);
    }

    public async Task<MonHocResponse> CreateAsync(CreateMonHocRequest request, CancellationToken ct)
    {
        await EnsureTeacherExistsIfProvidedAsync(request.GiaoVienId, ct);

        var monHoc = new MonHoc
        {
            Ten = request.Ten.Trim(),
            MaHocPhan = request.MaHocPhan.Trim(),
            TinChi = request.TinChi,
            GiaoVienId = request.GiaoVienId,
        };
        db.MonHocs.Add(monHoc);
        await db.SaveChangesAsync(ct);

        return (await ToResponsesAsync([monHoc], ct))[0];
    }

    public async Task<MonHocResponse> UpdateAsync(Guid id, UpdateMonHocRequest request, CancellationToken ct)
    {
        var monHoc = await db.MonHocs.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy môn học.");
        await EnsureTeacherExistsIfProvidedAsync(request.GiaoVienId, ct);

        monHoc.Ten = request.Ten.Trim();
        monHoc.MaHocPhan = request.MaHocPhan.Trim();
        monHoc.TinChi = request.TinChi;
        monHoc.GiaoVienId = request.GiaoVienId;
        await db.SaveChangesAsync(ct);

        return (await ToResponsesAsync([monHoc], ct))[0];
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var monHoc = await db.MonHocs.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy môn học.");
        db.MonHocs.Remove(monHoc);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MonHocResponse> AssignLopAsync(Guid id, List<Guid> lopIds, CancellationToken ct)
    {
        var monHoc = await db.MonHocs.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy môn học.");

        var distinctLopIds = lopIds.Distinct().ToList();
        if (distinctLopIds.Count > 0)
        {
            var validCount = await db.Lops.CountAsync(l => distinctLopIds.Contains(l.Id), ct);
            if (validCount != distinctLopIds.Count)
            {
                throw new NotFoundException("Một hoặc nhiều Lớp không tồn tại.");
            }
        }

        var current = await db.MonHocLops.Where(ml => ml.MonHocId == id).ToListAsync(ct);
        db.MonHocLops.RemoveRange(current);
        foreach (var lopId in distinctLopIds)
        {
            db.MonHocLops.Add(new MonHocLop { MonHocId = id, LopId = lopId });
        }
        await db.SaveChangesAsync(ct);

        return (await ToResponsesAsync([monHoc], ct))[0];
    }

    private async Task EnsureTeacherExistsIfProvidedAsync(Guid? giaoVienId, CancellationToken ct)
    {
        if (giaoVienId is null) return;
        var teacherExists = await db.Users.AnyAsync(u => u.Id == giaoVienId && u.Role == Roles.Teacher, ct);
        if (!teacherExists)
        {
            throw new NotFoundException("Không tìm thấy giáo viên.");
        }
    }

    private async Task<List<MonHocResponse>> ToResponsesAsync(List<MonHoc> monHocs, CancellationToken ct)
    {
        var monHocIds = monHocs.Select(m => m.Id).ToList();
        var giaoVienIds = monHocs.Where(m => m.GiaoVienId is not null).Select(m => m.GiaoVienId!.Value).Distinct().ToList();

        var giaoVienNames = await db.Users.Where(u => giaoVienIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var lopLinks = await db.MonHocLops.Where(ml => monHocIds.Contains(ml.MonHocId)).ToListAsync(ct);
        var lopIds = lopLinks.Select(ml => ml.LopId).Distinct().ToList();
        var lops = await db.Lops.Where(l => lopIds.Contains(l.Id)).ToListAsync(ct);
        var khoaIds = lops.Select(l => l.KhoaId).Distinct().ToList();
        var khoaNames = await db.Khoas.Where(k => khoaIds.Contains(k.Id)).ToDictionaryAsync(k => k.Id, k => k.Ten, ct);
        var lopById = lops.ToDictionary(l => l.Id);

        return monHocs.Select(m =>
        {
            var myLopIds = lopLinks.Where(ml => ml.MonHocId == m.Id).Select(ml => ml.LopId);
            var lopResponses = myLopIds
                .Where(lopById.ContainsKey)
                .Select(lopId =>
                {
                    var lop = lopById[lopId];
                    return new MonHocLopResponse(lop.Id, lop.Ten, lop.KhoaId, khoaNames.GetValueOrDefault(lop.KhoaId, "?"));
                })
                .ToList();

            return new MonHocResponse(
                m.Id, m.Ten, m.MaHocPhan, m.TinChi, m.GiaoVienId,
                m.GiaoVienId is not null ? giaoVienNames.GetValueOrDefault(m.GiaoVienId.Value) : null,
                lopResponses, m.CreatedAtUtc);
        }).ToList();
    }
}
