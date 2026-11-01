using System;
using System.Security.Cryptography;

namespace EventHub.Security
{
     
    public static class PasswordHasher
    {
        private const int Iterations = 100000;

        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(kdf.GetBytes(32));
            }
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            var parts = stored.Split('.');
            if (parts.Length != 2) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[0]);
                var expected = Convert.FromBase64String(parts[1]);
                byte[] actual;
                using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                    actual = kdf.GetBytes(expected.Length);

                var diff = 0;
                for (var i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
                return diff == 0;
            }
            catch (FormatException) { return false; }
        }
    }
}
