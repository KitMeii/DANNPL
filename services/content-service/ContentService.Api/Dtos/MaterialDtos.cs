namespace ContentService.Api.Dtos;

public sealed record CreateMaterialRequest(string Title, string? Chapter, string? Description, string FileName, string FileUrl, long FileSize, string? CloudinaryPublicId = null);

public sealed record UpdateMaterialRequest(string Title, string? Chapter, string? Description, bool IsActive);

public sealed record MaterialResponse(
    Guid Id,
    string Title,
    string? Chapter,
    string? Description,
    string FileName,
    string FileUrl,
    long FileSize,
    Guid UploadedBy,
    bool IsActive,
    int ViewCount,
    DateTime CreatedAtUtc);

/// <summary>Response for the raw-file upload step (POST /materials/upload) — the client then
/// passes FileUrl/FileName/FileSize/PublicId into CreateMaterialRequest to create the metadata
/// record. Two-step, mirroring the old Supabase Storage flow (upload bytes, then save metadata),
/// except both steps now go through content-service instead of the browser talking to storage
/// directly.</summary>
public sealed record UploadedFileResponse(string FileUrl, string FileName, long FileSize, string PublicId);
