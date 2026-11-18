using JQuery_ECommerceAss.Common.Security;

namespace Eduvo.Tests;

public class PasswordSecurityTests
{
    [Fact]
    public void HashCreatesVerifiableUniquePassword()
    {
        var first = PasswordSecurity.Hash("test-password");
        var second = PasswordSecurity.Hash("test-password");

        Assert.NotEqual(first, second);
        Assert.True(PasswordSecurity.Verify(first, "test-password", out _));
        Assert.False(PasswordSecurity.Verify(first, "wrong-password", out _));
    }

    [Fact]
    public void LegacyPasswordProducesUpgradeHash()
    {
        var valid = PasswordSecurity.Verify("legacy-password", "legacy-password", out var upgrade);

        Assert.True(valid);
        Assert.NotNull(upgrade);
        Assert.StartsWith("PBKDF2-SHA256$", upgrade);
        Assert.True(PasswordSecurity.Verify(upgrade!, "legacy-password", out _));
    }

    [Fact]
    public void MalformedHashFailsClosed()
    {
        Assert.False(PasswordSecurity.Verify("PBKDF2-SHA256$not-an-integer$salt$hash", "password", out _));
        Assert.False(PasswordSecurity.Verify("PBKDF2-SHA256$1000$!!!$!!!", "password", out _));
    }
}
