using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Infrastructure.Auth;

namespace QuizService.Tests.Integration;

public static class TestTokens
{
    private static readonly JwtTokenService TokenService = new(Options.Create(new JwtOptions
    {
        Issuer = "tthcm-platform",
        Audience = "tthcm-services",
        SigningKey = "dev-only-signing-key-do-not-use-in-production-min-32-chars",
    }));

    public static string For(string role, Guid? userId = null) =>
        TokenService.IssueAccessToken((userId ?? Guid.NewGuid()).ToString(), $"{role.ToLowerInvariant()}@test.local", $"{role} Test", role).AccessToken;

    public static string Student(Guid? userId = null) => For(Roles.Student, userId);
    public static string Teacher(Guid? userId = null) => For(Roles.Teacher, userId);
    public static string Admin(Guid? userId = null) => For(Roles.Admin, userId);
}
