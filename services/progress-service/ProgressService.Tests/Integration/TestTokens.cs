using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Infrastructure.Auth;

namespace ProgressService.Tests.Integration;

public static class TestTokens
{
    private static readonly JwtTokenService TokenService = new(Options.Create(new JwtOptions
    {
        Issuer = "tthcm-platform",
        Audience = "tthcm-services",
        SigningKey = "dev-only-signing-key-do-not-use-in-production-min-32-chars",
    }));

    private static string For(string role, Guid userId) =>
        TokenService.IssueAccessToken(userId.ToString(), $"{role.ToLowerInvariant()}@test.local", $"{role} Test", role).AccessToken;

    public static string Student(Guid userId) => For(Roles.Student, userId);

    // Việc B (2026-08-16) — cần token Teacher để test leaderboard lọc đúng role.
    public static string Teacher(Guid userId) => For(Roles.Teacher, userId);
}
