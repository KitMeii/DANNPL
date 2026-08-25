using Shared.Infrastructure.Auth;
using Xunit;

namespace AuthService.Tests.Unit;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verify_returns_true_for_correct_password()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd123");
        Assert.True(PasswordHasher.Verify("P@ssw0rd123", hash));
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        var hash = PasswordHasher.Hash("P@ssw0rd123");
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_is_salted_so_two_hashes_of_same_password_differ()
    {
        var hash1 = PasswordHasher.Hash("P@ssw0rd123");
        var hash2 = PasswordHasher.Hash("P@ssw0rd123");
        Assert.NotEqual(hash1, hash2);
    }
}
