using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Xunit;

namespace AuthService.Tests.Unit;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int accessTokenMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "tthcm-platform",
            Audience = "tthcm-services",
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
            AccessTokenMinutes = accessTokenMinutes,
        }));

    [Fact]
    public void IssueAccessToken_embeds_sub_email_name_and_role_claims()
    {
        var service = CreateService();
        var result = service.IssueAccessToken("user-123", "student@demo.tthcm", "Học viên Demo", Roles.Student);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal("user-123", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("student@demo.tthcm", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Học viên Demo", jwt.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal(Roles.Student, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void IssueAccessToken_sets_issuer_and_audience()
    {
        var service = CreateService();
        var result = service.IssueAccessToken("user-123", "a@b.com", "A", Roles.Teacher);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal("tthcm-platform", jwt.Issuer);
        Assert.Contains("tthcm-services", jwt.Audiences);
    }

    [Fact]
    public void IssueAccessToken_expiry_matches_configured_access_token_minutes()
    {
        var service = CreateService(accessTokenMinutes: 15);
        var before = DateTime.UtcNow;

        var result = service.IssueAccessToken("user-123", "a@b.com", "A", Roles.Admin);

        var expectedExpiry = before.AddMinutes(15);
        Assert.True(Math.Abs((result.ExpiresAtUtc - expectedExpiry).TotalSeconds) < 5);
    }
}
