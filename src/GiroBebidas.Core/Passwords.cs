using System.Security.Cryptography;

namespace GiroBebidas.Core;

public static class Passwords
{
    public const int Iterations = 210_000;
    public static void Set(UserAccount user, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4 || password.Length > 128)
            throw new InvalidOperationException("A senha deve ter entre 4 e 128 caracteres.");
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        user.PasswordSalt = Convert.ToBase64String(salt);
        user.PasswordIterations = Iterations;
        user.PasswordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32));
    }
    public static bool Verify(UserAccount user, string password)
    {
        try
        {
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(user.PasswordSalt), user.PasswordIterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(user.PasswordHash));
        }
        catch (FormatException) { return false; }
    }
}
