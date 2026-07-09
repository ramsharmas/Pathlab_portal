using System;
using System.Security.Cryptography;
using System.Text;

namespace PathlabWcfService.Helpers
{
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, 10);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        public static string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        public static string GenerateBookingRef()
        {
            return "BK" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString().Substring(3);
        }
    }
}
