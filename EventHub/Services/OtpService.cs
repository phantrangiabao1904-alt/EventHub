using System;
using System.Security.Cryptography;
using EventHub.Security;

namespace EventHub.Services
{
    public static class OtpService
    {
         
        public static string Generate()
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var n = BitConverter.ToUInt32(bytes, 0) % 1000000u;
            return n.ToString("D6");
        }

        public static string Hash(string code) { return PasswordHasher.Hash(code); }

        public static bool Verify(string code, string hash)
        {
            return PasswordHasher.Verify((code ?? "").Trim(), hash);
        }
    }
}
