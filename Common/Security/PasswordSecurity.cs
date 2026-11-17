using System.Security.Cryptography;
using System.Text;

namespace JQuery_ECommerceAss.Common.Security
{
    public static class PasswordSecurity
    {
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const string Prefix = "PBKDF2-SHA256";

        public static string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string storedValue, string suppliedPassword, out string? upgradedHash)
        {
            upgradedHash = null;

            if (string.IsNullOrEmpty(storedValue) || string.IsNullOrEmpty(suppliedPassword))
            {
                return false;
            }

            if (!storedValue.StartsWith($"{Prefix}$", StringComparison.Ordinal))
            {
                var legacyMatch = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(storedValue),
                    Encoding.UTF8.GetBytes(suppliedPassword));

                if (legacyMatch)
                {
                    upgradedHash = Hash(suppliedPassword);
                }

                return legacyMatch;
            }

            var parts = storedValue.Split('$');
            if (parts.Length != 4 ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations <= 0)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedHash = Convert.FromBase64String(parts[3]);
                var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    suppliedPassword,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
