using AuthService.Api.Data;
using AuthService.Api.Dtos;
using AuthService.Api.Services;
using AuthService.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Xunit;

namespace AuthService.Tests.Unit;

public sealed class AuthServiceImplTests
{
    private static (AuthDbContext Db, IAuthService Sut) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AuthDbContext(options);

        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "tthcm-platform",
            Audience = "tthcm-services",
            SigningKey = "unit-test-signing-key-at-least-32-characters-long",
        }));

        return (db, new AuthServiceImpl(db, tokenService, new FakeAvatarStorage()));
    }

    [Fact]
    public async Task RegisterAsync_creates_user_with_Student_role_regardless_of_input()
    {
        var (_, sut) = CreateSut();

        var result = await sut.RegisterAsync(new RegisterRequest("new@test.local", "P@ssw0rd123", "New User"), default);

        Assert.Equal(Roles.Student, result.User.Role);
    }

    [Fact]
    public async Task RegisterAsync_with_duplicate_email_throws_ConflictException()
    {
        var (_, sut) = CreateSut();
        await sut.RegisterAsync(new RegisterRequest("dup@test.local", "P@ssw0rd123", "First"), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.RegisterAsync(new RegisterRequest("dup@test.local", "AnotherPass1", "Second"), default));
    }

    [Fact]
    public async Task LoginAsync_with_correct_credentials_returns_matching_user()
    {
        var (_, sut) = CreateSut();
        await sut.RegisterAsync(new RegisterRequest("login@test.local", "P@ssw0rd123", "Login User"), default);

        var result = await sut.LoginAsync(new LoginRequest("login@test.local", "P@ssw0rd123"), default);

        Assert.Equal("login@test.local", result.User.Email);
        Assert.NotEmpty(result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_with_wrong_password_throws_AuthenticationFailedException()
    {
        var (_, sut) = CreateSut();
        await sut.RegisterAsync(new RegisterRequest("login2@test.local", "P@ssw0rd123", "Login User"), default);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.LoginAsync(new LoginRequest("login2@test.local", "wrong-password"), default));
    }

    [Fact]
    public async Task LoginAsync_with_unknown_email_throws_AuthenticationFailedException()
    {
        var (_, sut) = CreateSut();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            sut.LoginAsync(new LoginRequest("nobody@test.local", "whatever123"), default));
    }

    [Fact]
    public async Task GetByIdAsync_with_unknown_id_throws_NotFoundException()
    {
        var (_, sut) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetByIdAsync(Guid.NewGuid(), default));
    }
}
