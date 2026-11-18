using System.Security.Cryptography;
using System.Text.Json;

namespace JQuery_ECommerceAss.Common.Security;

internal sealed class AccountVerificationCode(string sessionKey)
{
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private sealed record Challenge(string Code, string Purpose, DateTimeOffset ExpiresAt, int AttemptsRemaining);

    public static string Generate() => RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

    public void Issue(ISession session, string code, string purpose)
    {
        Store(session, new Challenge(code, purpose, DateTimeOffset.UtcNow + Lifetime, MaximumAttempts));
    }

    public bool IsActive(ISession session) => Read(session) is not null;

    public string? Verify(ISession session, string code)
    {
        var challenge = Read(session);
        if (challenge is null)
        {
            return null;
        }

        if (string.Equals(challenge.Code, code, StringComparison.Ordinal))
        {
            Clear(session);
            return challenge.Purpose;
        }

        if (challenge.AttemptsRemaining == 1)
        {
            Clear(session);
        }
        else
        {
            Store(session, challenge with { AttemptsRemaining = challenge.AttemptsRemaining - 1 });
        }
        return null;
    }

    private Challenge? Read(ISession session)
    {
        var stored = session.GetString(sessionKey);
        var challenge = stored is null ? null : JsonSerializer.Deserialize<Challenge>(stored);
        if (challenge?.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Clear(session);
            return null;
        }
        return challenge;
    }

    private void Store(ISession session, Challenge challenge) =>
        session.SetString(sessionKey, JsonSerializer.Serialize(challenge));

    private void Clear(ISession session) => session.Remove(sessionKey);
}
