using System.Security.Cryptography;
using System.Text;

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha1.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public static bool VerifyPassword(string enteredPassword, string storedHashedPassword)
    {
        if (string.IsNullOrEmpty(storedHashedPassword))
            return false;

        string sha1Hash = HashPassword(enteredPassword);
        return string.Equals(sha1Hash, storedHashedPassword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHashed(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Length == 40 && value.All(c => Uri.IsHexDigit(c));
    }
}
