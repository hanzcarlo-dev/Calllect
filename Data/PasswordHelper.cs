
using System.Security.Cryptography;

namespace CallLect.Data
{
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );

            return Convert.ToBase64String(salt) + "." +
                   Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(
            string password,
            string storedPassword)
        {
            string[] parts = storedPassword.Split('.');

            if (parts.Length != 2)
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedHash = Convert.FromBase64String(parts[1]);

                byte[] enteredHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    100000,
                    HashAlgorithmName.SHA256,
                    32
                );

                return CryptographicOperations.FixedTimeEquals(
                    storedHash,
                    enteredHash
                );
            }
            catch
            {
                return false;
            }
        }
    }
}
