using ContentService.Api.Data;
using ContentService.Api.Dtos;
using ContentService.Api.Entities;
using ContentService.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace ContentService.Api.Services;

public sealed class MaterialService(ContentDbContext db, IFileStorage fileStorage, ILogger<MaterialService> logger) : IMaterialService
{
    /// <summary>Rà soát Lần VIII (2026-08-21) — callerUserId/callerRole THÊM MỚI: khi Teacher gọi
    /// (đang quản lý "Tài liệu bài giảng" của chính mình), chỉ trả tài liệu do CHÍNH GV đó tải lên
    /// (trước đây dùng chung, GV này thấy/sửa/xóa được của GV khác — xác nhận lỗi thật qua yêu cầu
    /// người dùng). Student/Admin KHÔNG lọc — endpoint này CŨNG là nguồn danh sách tài liệu học viên
    /// xem ở "Giảng bài" (không có RequireRole ở GET /), lọc theo người tải lên sẽ vô tình giấu mất
    /// tài liệu học viên cần thấy chỉ vì khác GV tải lên.</summary>
    public async Task<IReadOnlyList<MaterialResponse>> ListAsync(bool includeInactive, string? chapter, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.Materials.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(m => m.Chapter == chapter);
        }

        if (callerRole == Roles.Teacher)
        {
            query = query.Where(m => m.UploadedBy == callerUserId);
        }

        var materials = await query.OrderBy(m => m.Chapter).ThenByDescending(m => m.CreatedAtUtc).ToListAsync(ct);
        return materials.Select(ToResponse).ToList();
    }

    public async Task<MaterialResponse> GetByIdAsync(Guid id, bool includeInactive, CancellationToken ct)
    {
        var material = await db.Materials.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        if (!includeInactive && !material.IsActive)
        {
            throw new NotFoundException("Không tìm thấy tài liệu.");
        }

        return ToResponse(material);
    }

    public async Task<MaterialResponse> CreateAsync(CreateMaterialRequest request, Guid uploadedBy, CancellationToken ct)
    {
        var material = new Material
        {
            Title = request.Title.Trim(),
            Chapter = request.Chapter?.Trim(),
            Description = request.Description?.Trim(),
            FileName = request.FileName.Trim(),
            FileUrl = request.FileUrl.Trim(),
            FileSize = request.FileSize,
            CloudinaryPublicId = request.CloudinaryPublicId,
            UploadedBy = uploadedBy,
        };

        db.Materials.Add(material);
        await db.SaveChangesAsync(ct);
        return ToResponse(material);
    }

    public async Task<MaterialResponse> UpdateAsync(Guid id, UpdateMaterialRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var material = await db.Materials.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");
        EnsureOwnerOrAdmin(material, callerUserId, callerRole);

        material.Title = request.Title.Trim();
        material.Chapter = request.Chapter?.Trim();
        material.Description = request.Description?.Trim();
        material.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return ToResponse(material);
    }

    public async Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var material = await db.Materials.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");
        EnsureOwnerOrAdmin(material, callerUserId, callerRole);

        if (!string.IsNullOrWhiteSpace(material.CloudinaryPublicId))
        {
            try
            {
                await fileStorage.DeleteAsync(material.CloudinaryPublicId, ct);
            }
            catch (Exception ex)
            {
                // Best-effort: an orphaned file in Cloudinary is a minor cleanup issue, not a
                // reason to block the user from deleting the material record.
                logger.LogWarning(ex, "Failed to delete Cloudinary file {PublicId} for material {MaterialId}", material.CloudinaryPublicId, id);
            }
        }

        db.Materials.Remove(material);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> IncrementViewCountAsync(Guid id, CancellationToken ct)
    {
        var material = await db.Materials.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        material.ViewCount++;
        await db.SaveChangesAsync(ct);
        return material.ViewCount;
    }

    private static MaterialResponse ToResponse(Material m) => new(
        m.Id, m.Title, m.Chapter, m.Description, m.FileName, m.FileUrl, m.FileSize,
        m.UploadedBy, m.IsActive, m.ViewCount, m.CreatedAtUtc);

    /// <summary>Rà soát Lần VIII (2026-08-21) — chỉ người tải lên (UploadedBy) hoặc Admin được sửa/
    /// xóa 1 tài liệu — cùng pattern QuestionService.EnsureOwnerOrAdmin.</summary>
    private static void EnsureOwnerOrAdmin(Material material, Guid callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin) return;
        if (material.UploadedBy == callerUserId) return;
        throw new UnauthorizedAccessException("Bạn chỉ được sửa/xóa tài liệu do chính mình tải lên.");
    }
}
